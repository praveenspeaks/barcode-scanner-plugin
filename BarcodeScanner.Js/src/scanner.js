/**
 * BarcodeScannerPlugin — JavaScript SDK
 * Works with BarcodeScanner.Api backend.
 */

class BarcodeScannerPlugin {
  /**
   * @param {object} options
   * @param {string} options.apiUrl - Base URL of BarcodeScanner.Api (e.g. "http://localhost:5000")
   * @param {string} [options.videoElementId] - ID of <video> element for camera mode
   * @param {string} [options.canvasElementId] - ID of <canvas> used for frame capture
   * @param {number} [options.scanInterval=500] - ms between auto-scans in live mode
   * @param {function} [options.onResult] - callback(result) fired when barcodes found
   * @param {function} [options.onError] - callback(error)
   */
  constructor(options = {}) {
    this.apiUrl = (options.apiUrl || 'http://localhost:5000').replace(/\/$/, '');
    this.videoElementId = options.videoElementId || null;
    this.canvasElementId = options.canvasElementId || null;
    this.scanInterval = options.scanInterval ?? 500;
    this.onResult = options.onResult || null;
    this.onError = options.onError || null;

    this._stream = null;
    this._intervalId = null;
    this._canvas = null;
    this._video = null;
  }

  // ─── Static image scanning ─────────────────────────────────────────────────

  /**
   * Scan a File or Blob object.
   * @param {File|Blob} file
   * @returns {Promise<ScanResult>}
   */
  async scanFile(file) {
    const formData = new FormData();
    formData.append('file', file);
    return this._post('/api/scan/upload', formData, false);
  }

  /**
   * Scan an <img> element or an image URL (fetched via canvas).
   * @param {HTMLImageElement|string} imgOrUrl
   * @returns {Promise<ScanResult>}
   */
  async scanImage(imgOrUrl) {
    const img = typeof imgOrUrl === 'string'
      ? await this._loadImage(imgOrUrl)
      : imgOrUrl;

    const canvas = document.createElement('canvas');
    canvas.width = img.naturalWidth || img.width;
    canvas.height = img.naturalHeight || img.height;
    canvas.getContext('2d').drawImage(img, 0, 0);
    const base64 = canvas.toDataURL('image/png');
    return this.scanBase64(base64);
  }

  /**
   * Scan a base64-encoded image string (with or without data: prefix).
   * @param {string} base64
   * @returns {Promise<ScanResult>}
   */
  async scanBase64(base64) {
    return this._post('/api/scan/base64', { image: base64 }, true);
  }

  // ─── Live camera scanning ───────────────────────────────────────────────────

  /**
   * Start scanning from the device camera.
   * Fires onResult callback whenever barcodes are detected.
   */
  async startCamera(facingMode = 'environment') {
    if (!this.videoElementId) throw new Error('videoElementId is required for camera mode.');
    if (!this.canvasElementId) throw new Error('canvasElementId is required for camera mode.');

    this._video = document.getElementById(this.videoElementId);
    this._canvas = document.getElementById(this.canvasElementId);

    this._stream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode }
    });
    this._video.srcObject = this._stream;
    await this._video.play();

    this._intervalId = setInterval(() => this._scanFrame(), this.scanInterval);
  }

  /** Stop live camera scanning and release the camera. */
  stopCamera() {
    if (this._intervalId) clearInterval(this._intervalId);
    if (this._stream) this._stream.getTracks().forEach(t => t.stop());
    this._stream = null;
    this._intervalId = null;
  }

  /** Capture a single frame from the running camera and scan it. */
  async captureFrame() {
    if (!this._video || !this._canvas) throw new Error('Camera not started.');
    return this._scanFrame();
  }

  // ─── Internal ───────────────────────────────────────────────────────────────

  async _scanFrame() {
    const ctx = this._canvas.getContext('2d');
    this._canvas.width = this._video.videoWidth;
    this._canvas.height = this._video.videoHeight;
    ctx.drawImage(this._video, 0, 0);
    const base64 = this._canvas.toDataURL('image/jpeg', 0.85);

    try {
      const result = await this.scanBase64(base64);
      if (result.success && result.barcodes.length > 0 && this.onResult) {
        this.onResult(result);
      }
      return result;
    } catch (err) {
      if (this.onError) this.onError(err);
    }
  }

  async _post(path, body, isJson) {
    const opts = {
      method: 'POST',
      body: isJson ? JSON.stringify(body) : body,
    };
    if (isJson) opts.headers = { 'Content-Type': 'application/json' };

    const res = await fetch(this.apiUrl + path, opts);
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`BarcodeScanner API error ${res.status}: ${text}`);
    }
    return res.json();
  }

  _loadImage(url) {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.crossOrigin = 'anonymous';
      img.onload = () => resolve(img);
      img.onerror = reject;
      img.src = url;
    });
  }
}

// ─── Health check utility ────────────────────────────────────────────────────

BarcodeScannerPlugin.checkHealth = async function (apiUrl) {
  const url = apiUrl.replace(/\/$/, '') + '/api/scan/health';
  const res = await fetch(url);
  return res.json();
};

// Export for both ES modules and plain <script> tags
if (typeof module !== 'undefined' && module.exports) {
  module.exports = BarcodeScannerPlugin;
} else if (typeof window !== 'undefined') {
  window.BarcodeScannerPlugin = BarcodeScannerPlugin;
}
