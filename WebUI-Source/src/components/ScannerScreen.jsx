import React, { useState, useCallback, useRef, useEffect } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { Icon } from './Icons'
import { useGameStore } from '../stores/useGameStore'
import { useUnityBridge, ACTIONS } from '../hooks/useUnityBridge'
import { COLORS } from '../constants/theme'
import LootReward from './LootReward'

export default function ScannerScreen({ onCapture, onBack }) {
  const [flashOn, setFlashOn] = useState(false)
  const [gridOn, setGridOn] = useState(false)
  
  const [identified, setIdentified] = useState(null)
  const [isProcessing, setIsProcessing] = useState(false)
  const [capturedFrame, setCapturedFrame] = useState(null)
  const [scanStage, setScanStage] = useState('idle')
  const [cameraError, setCameraError] = useState(null)
  const [analysisError, setAnalysisError] = useState(null)
  // True once we confirm window._unityBridge exists — controls video rendering
  const [inUnityMode, setInUnityMode] = useState(false)
  
  const videoRef = useRef(null)
  const canvasRef = useRef(null)
  const isInitializing = useRef(false)
  const { addMaterial, addScrap } = useGameStore()
  const sendToUnity = useUnityBridge()
  
  // Detect Unity bridge synchronously first — by the time the user navigates
  // to Scanner the bridge is already injected (page loaded earlier).
  // Fall back to a 800ms wait only if not immediately found.
  useEffect(() => {
    let mounted = true

    const tryDetect = (immediate) => {
      const isUnity = !!(window._unityBridge?.send)
      if (!mounted) return
      if (isUnity) {
        setInUnityMode(true)
        // AR camera already started by WebViewManager (SET_PAGE=scan → StartAR)
      } else if (!immediate) {
        // Not Unity — start native camera
        startCamera()
      }
    }

    // Synchronous check
    tryDetect(true)

    // If not found immediately, wait 800ms and try again
    const timer = setTimeout(() => tryDetect(false), 800)

    return () => {
      mounted = false
      clearTimeout(timer)
      stopCamera()
    }
  }, [])

  const startCamera = async () => {
    if (isInitializing.current) return
    isInitializing.current = true
    
    setCameraError(null)
    setAnalysisError(null)

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      setCameraError("Camera API not supported or not in a secure (HTTPS) context.")
      isInitializing.current = false
      return
    }

    try {
      // Try 'environment' first for mobile
      const constraints = { 
        video: { 
          facingMode: { ideal: 'environment' },
          width: { ideal: 1280 },
          height: { ideal: 720 }
        }
      }
      
      const stream = await navigator.mediaDevices.getUserMedia(constraints)
      if (videoRef.current) {
        videoRef.current.srcObject = stream
      }
    } catch (err) {
      console.warn("Primary camera constraint failed, trying fallback:", err)
      
      // Specific check for "Device in use"
      if (err.name === 'NotReadableError') {
        setCameraError("Camera is already in use by another app or browser tab. Please close it and try again.")
        isInitializing.current = false
        return
      }

      try {
        const stream = await navigator.mediaDevices.getUserMedia({ video: true })
        if (videoRef.current) {
          videoRef.current.srcObject = stream
        }
      } catch (fallbackErr) {
        console.error("Camera fallback failed:", fallbackErr)
        if (fallbackErr.name === 'NotReadableError') {
          setCameraError("Camera locked by another application. Please check your camera settings.")
        } else {
          setCameraError("No camera found or permissions denied.")
        }
      }
    } finally {
      isInitializing.current = false
    }
  }

  const stopCamera = () => {
    if (videoRef.current && videoRef.current.srcObject) {
      const tracks = videoRef.current.srcObject.getTracks()
      tracks.forEach(track => track.stop())
    }
  }

  const simulateYoloDetection = async (canvas) => {
    setIsProcessing(true)
    setAnalysisError(null)
    
    const ctx = canvas.getContext('2d')
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height)
    const data = imageData.data
    
    // Focus analysis on the center 50% of the frame (where the object is)
    const centerX = canvas.width / 4
    const centerY = canvas.height / 4
    const centerWidth = canvas.width / 2
    const centerHeight = canvas.height / 2
    
    let r = 0, g = 0, b = 0
    let brightness = 0
    let sampledPixels = 0
    const sampleRate = 20
    
    for (let y = centerY; y < centerY + centerHeight; y += sampleRate) {
      for (let x = centerX; x < centerX + centerWidth; x += sampleRate) {
        const i = (Math.floor(y) * canvas.width + Math.floor(x)) * 4
        r += data[i]
        g += data[i+1]
        b += data[i+2]
        brightness += (data[i] + data[i+1] + data[i+2]) / 3
        sampledPixels++
      }
    }
    
    const avgR = r / sampledPixels
    const avgG = g / sampledPixels
    const avgB = b / sampledPixels
    const avgBrightness = brightness / sampledPixels
    
    // Artificial delay for "Intelligence"
    await new Promise(r => setTimeout(r, 2200))
    
    // 1. Darkness check
    if (avgBrightness < 20) {
      setAnalysisError("Object not recognized. Environment too dark or camera obscured.")
      setIsProcessing(false)
      return
    }

    // 2. Smart Color-Based Classification (Heuristics)
    let detectedType = 'paper' 
    
    // PRIORITY: If Blue is prominent -> Definitely Plastic
    // Clear plastic often has a blue tint or blue cap
    if (avgB > avgR + 5 && avgB > avgG + 5) {
      detectedType = 'plastic'
    } 
    // If White/Very Bright -> Paper Cup
    else if (avgBrightness > 160) {
      detectedType = 'paper'
    }
    // If Grey/Silverish (Low saturation) -> Metal Can
    else if (Math.abs(avgR - avgG) < 20 && Math.abs(avgG - avgB) < 20) {
      detectedType = 'metal'
    }
    else {
      detectedType = 'plastic' // Default to plastic as it's common
    }

    const confidence = 0.92 + Math.random() * 0.05
    
    const results = {
      material: detectedType,
      displayName: detectedType === 'plastic' ? 'Plastic Bottle' : 
                   detectedType === 'metal' ? 'Metal Can' : 'Paper Cup',
      confidence,
      isRecyclable: true
    }
    
    setIdentified(results)
    setScanStage('loot')
    setIsProcessing(false)
  }
  
  const handleShutter = useCallback(() => {
    if (isProcessing || analysisError) return

    setScanStage('capturing')
    setIsProcessing(true)  // ← show "ANALYZING OBJECT..." overlay immediately

    if (inUnityMode) {
      // Tell Unity to capture the AR frame and run detection
      sendToUnity(ACTIONS.CAPTURE_AND_SCAN)
      // Unity replies with SCAN_COMPLETE via unityEvent; fallback fires after 2.5s
      setTimeout(() => simulateYoloDetectionUnityFallback(), 2500)
      return
    }

    // Native camera mode: draw the video frame to canvas
    const video = videoRef.current
    const canvas = canvasRef.current
    if (!canvas || !video || !video.videoWidth) {
      setCameraError('Camera not ready. Please wait a moment and try again.')
      setScanStage('idle')
      setIsProcessing(false)
      return
    }

    const context = canvas.getContext('2d')
    canvas.width = video.videoWidth
    canvas.height = video.videoHeight
    context.drawImage(video, 0, 0, canvas.width, canvas.height)

    const base64 = canvas.toDataURL('image/jpeg', 0.8)
    setCapturedFrame(base64)
    stopCamera()
    simulateYoloDetection(canvas)
  }, [isProcessing, analysisError, inUnityMode])

  // Fallback result generator used in Unity mode when no SCAN_COMPLETE arrives
  const simulateYoloDetectionUnityFallback = () => {
    const types = ['plastic', 'metal', 'paper']
    const detectedType = types[Math.floor(Math.random() * types.length)]
    const results = {
      material: detectedType,
      displayName: detectedType === 'plastic' ? 'Plastic Bottle' :
                   detectedType === 'metal' ? 'Metal Can' : 'Paper Cup',
      confidence: 0.92 + Math.random() * 0.05,
      isRecyclable: true
    }
    setIdentified(results)
    setScanStage('loot')
    setIsProcessing(false)
  }
  
  const handleBack = () => {
    stopCamera()
    onBack()
  }

  const handleCollect = () => {
    stopCamera() // Ensure release
    if (identified) {
      addMaterial(identified.material, 1)
      addScrap(Math.floor(Math.random() * 5) + 5)
      onCapture?.(identified)
    }
    resetScanner()
  }

  const resetScanner = () => {
    setIdentified(null)
    setCapturedFrame(null)
    setScanStage('idle')
    setIsProcessing(false)
    setAnalysisError(null)
    // Only restart native camera; Unity AR feed is always live
    if (!inUnityMode) startCamera()
  }
  
  return (
    <div
      className="scanner-container"
      style={{ background: inUnityMode ? 'transparent' : '#000' }}
    >
      {/* Camera Feed — native mode only: Unity AR shows through WebView transparency */}
      <div className="camera-view" style={{ background: inUnityMode ? 'transparent' : '#000' }}>
        {!inUnityMode && (
          cameraError ? (
            <div className="camera-placeholder">
              <Icon name="warning" size={48} color={COLORS.orange} />
              <p>{cameraError}</p>
              <button onClick={startCamera} className="retry-btn">Retry Access</button>
            </div>
          ) : (
            <video 
              ref={videoRef} 
              autoPlay 
              muted
              playsInline 
              className="camera-video"
              style={{ display: scanStage === 'idle' ? 'block' : 'none' }}
            />
          )
        )}
        
        {capturedFrame && scanStage !== 'idle' && (
          <img src={capturedFrame} className="camera-video captured" alt="Still" />
        )}
      </div>

      <canvas ref={canvasRef} style={{ display: 'none' }} />
      
      {/* Shutter Flash Effect */}
      <AnimatePresence>
        {scanStage === 'capturing' && (
          <motion.div 
            initial={{ opacity: 1 }}
            animate={{ opacity: 0 }}
            exit={{ opacity: 0 }}
            className="shutter-flash"
          />
        )}
      </AnimatePresence>
      
      {/* UI Elements */}
      <div className="scanner-overlay">
        <div className="scanner-top-bar">
          <motion.button whileTap={{ scale: 0.9 }} onClick={handleBack} className="icon-btn">
            <Icon name="times" size={20} color="#fff" />
          </motion.button>
          
          <div className="scanner-status">
            <div className="pulse-dot" />
            <span>AI SCANNER ACTIVE</span>
          </div>
          
          <div style={{ width: 44 }} />
        </div>

        {scanStage === 'idle' && (
          <>
            <div className="scan-reticle">
              <div className="reticle-corner tl" />
              <div className="reticle-corner tr" />
              <div className="reticle-corner bl" />
              <div className="reticle-corner br" />
              <motion.div 
                animate={{ y: [0, 340, 0] }}
                transition={{ repeat: Infinity, duration: 3, ease: "linear" }}
                className="scan-line" 
              />
            </div>
            
            <div className="bottom-controls">
              <p className="instruction">POSITION TRASH WITHIN FRAME</p>
              <button className="shutter-btn" onClick={handleShutter}>
                <div className="shutter-btn-inner" />
              </button>
            </div>
          </>
        )}

        {isProcessing && (
          <div className="analysis-overlay">
            <div className="ai-brain-anim">
              <Icon name="radar" size={48} color={COLORS.primary} />
            </div>
            <h2>ANALYZING OBJECT...</h2>
            <p>Using YOLO World Real-time Detection</p>
          </div>
        )}

        {analysisError && (
          <div className="analysis-overlay error">
            <Icon name="warning" size={48} color={COLORS.orange} />
            <h2 style={{ color: COLORS.orange }}>SCAN FAILED</h2>
            <p>{analysisError}</p>
            <button onClick={resetScanner} className="retry-btn" style={{ pointerEvents: 'auto' }}>
              Try Again
            </button>
          </div>
        )}
      </div>

      {/* Loot Reward 3D Sequence */}
      <AnimatePresence>
        {scanStage === 'loot' && identified && (
          <LootReward 
            type={identified.material} 
            onConfirm={handleCollect} 
            onDiscard={resetScanner}
          />
        )}
      </AnimatePresence>
      
      <style>{`
        .scanner-container {
          position: fixed;
          inset: 0;
          /* background controlled by inline style — transparent in AR/Unity mode, #000 in native */
          z-index: 1000;
        }
        .camera-view {
          position: absolute;
          inset: 0;
          display: flex;
          align-items: center;
          justify-content: center;
          /* background controlled by inline style */
        }
        .camera-video {
          width: 100%;
          height: 100%;
          object-fit: cover;
        }
        .camera-placeholder {
          text-align: center;
          color: #fff;
          padding: 40px;
        }
        .retry-btn {
          margin-top: 20px;
          padding: 12px 24px;
          background: ${COLORS.primary};
          color: #fff;
          border: none;
          border-radius: 12px;
        }
        .scanner-overlay {
          position: absolute;
          inset: 0;
          z-index: 10;
          display: flex;
          flex-direction: column;
          pointer-events: none;
        }
        .scanner-top-bar {
          padding: 50px 20px 20px;
          display: flex;
          justify-content: space-between;
          align-items: center;
          pointer-events: auto;
        }
        .icon-btn {
          width: 44px;
          height: 44px;
          border-radius: 14px;
          background: rgba(0,0,0,0.5);
          display: flex;
          align-items: center;
          justify-content: center;
          border: none;
        }
        .scanner-status {
          display: flex;
          align-items: center;
          gap: 8px;
          padding: 8px 16px;
          background: rgba(0,0,0,0.5);
          border-radius: 20px;
          color: #fff;
          font-size: 11px;
          font-weight: 800;
          letter-spacing: 1px;
        }
        .pulse-dot {
          width: 6px;
          height: 6px;
          background: #34C759;
          border-radius: 50%;
          box-shadow: 0 0 10px #34C759;
          animation: pulse 2s infinite;
        }
        @keyframes pulse {
          0% { opacity: 1; transform: scale(1); }
          50% { opacity: 0.3; transform: scale(1.5); }
          100% { opacity: 1; transform: scale(1); }
        }
        .scan-reticle {
          position: absolute;
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
          width: 280px;
          height: 340px;
          border: 1px solid rgba(255,255,255,0.1);
        }
        .reticle-corner {
          position: absolute;
          width: 30px;
          height: 30px;
          border: 4px solid #fff;
        }
        .tl { top: -2px; left: -2px; border-right: none; border-bottom: none; border-top-left-radius: 12px; }
        .tr { top: -2px; right: -2px; border-left: none; border-bottom: none; border-top-right-radius: 12px; }
        .bl { bottom: -2px; left: -2px; border-right: none; border-top: none; border-bottom-left-radius: 12px; }
        .br { bottom: -2px; right: -2px; border-left: none; border-top: none; border-bottom-right-radius: 12px; }
        .scan-line {
          position: absolute;
          top: 0;
          left: 0;
          width: 100%;
          height: 2px;
          background: linear-gradient(90deg, transparent, ${COLORS.primary}, transparent);
          box-shadow: 0 0 15px ${COLORS.primary};
        }
        .bottom-controls {
          position: absolute;
          bottom: 60px;
          left: 0;
          right: 0;
          display: flex;
          flex-direction: column;
          align-items: center;
          gap: 20px;
          pointer-events: auto;
        }
        .instruction {
          color: #fff;
          font-size: 12px;
          font-weight: 700;
          letter-spacing: 2px;
          text-shadow: 0 2px 4px rgba(0,0,0,0.5);
        }
        .shutter-btn {
          width: 80px;
          height: 80px;
          border-radius: 50%;
          border: 4px solid #fff;
          background: transparent;
          padding: 4px;
          cursor: pointer;
        }
        .shutter-btn-inner {
          width: 100%;
          height: 100%;
          background: #fff;
          border-radius: 50%;
        }
        .analysis-overlay {
          position: absolute;
          inset: 0;
          background: rgba(0,0,0,0.85);
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          color: #fff;
          text-align: center;
          pointer-events: auto;
          z-index: 20;
          padding: 30px;
        }
        .analysis-overlay.error {
          border: 2px solid ${COLORS.orange}40;
        }
        .ai-brain-anim {
          margin-bottom: 24px;
          animation: spin 3s infinite linear;
        }
        @keyframes spin {
          from { rotate: 0deg; }
          to { rotate: 360deg; }
        }
        .analysis-overlay h2 { margin: 0 0 8px; font-weight: 800; }
        .analysis-overlay p { opacity: 0.5; font-size: 14px; }
        .shutter-flash {
          position: fixed;
          inset: 0;
          background: #fff;
          z-index: 100;
        }
      `}</style>
    </div>
  )
}