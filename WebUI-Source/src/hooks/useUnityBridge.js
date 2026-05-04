import { useEffect, useCallback } from 'react'

// ─── Action constants — must match UnityBridge.cs ─────────────────────────
export const ACTIONS = {
  // React → Unity
  SPAWN_BOT:          'SPAWN_BOT',
  DESPAWN_BOT:        'DESPAWN_BOT',
  START_SCAN:         'START_SCAN',
  STOP_SCAN:          'STOP_SCAN',
  CAPTURE_AND_SCAN:   'CAPTURE_AND_SCAN',
  SCAN_FRAME_B64:     'SCAN_FRAME_B64',   // React-captured frame → Unity → Roboflow
  REQUEST_SCAN_STATE: 'REQUEST_SCAN_STATE',
  SELECT_CATEGORY:    'SELECT_CATEGORY',
  CONFIRM_DEPLOY:     'CONFIRM_DEPLOY',
  SET_PAGE:           'SET_PAGE',
  INITIATE_AR:        'INITIATE_AR',

  // Unity → React
  SCAN_COMPLETE:      'SCAN_COMPLETE',
  SCAN_PROCESSING:    'SCAN_PROCESSING',
  SCAN_EMPTY:         'SCAN_EMPTY',
  FRAME_CAPTURED:     'FRAME_CAPTURED',
  BOT_SPAWNED:        'BOT_SPAWNED',
  BOT_DESPAWNED:      'BOT_DESPAWNED',
  TRACKING_CHANGED:   'TRACKING_CHANGED',
  ERROR:              'ERROR',
}

/**
 * useUnityBridge — React hook for the Gree WebView bi-directional bridge.
 *
 * @param {Object} handlers  Map of action → callback for Unity → React events.
 *                           Example: { [ACTIONS.SCAN_COMPLETE]: (payload) => ... }
 *
 * @returns {Function} sendToUnity(action, payload) — call this to send React → Unity.
 *
 * When running inside Unity's Gree WebView:
 *   - Outgoing: window._unityBridge.send(action, payload)  (injected by WebViewManager.cs)
 *   - Incoming: window.dispatchUnityEvent fires a CustomEvent('unityEvent')
 *
 * When running in a desktop browser (development / preview):
 *   - The polyfill in index.html stubs both globals, logging to console.
 */
export function useUnityBridge(handlers = {}) {
  // ── Listen for incoming Unity → React events ──────────────────────────
  useEffect(() => {
    const handleUnityEvent = (e) => {
      const { action, payload } = e.detail || {}
      if (!action) return

      const handler = handlers[action]
      if (handler) {
        try {
          // payload is a JSON string from Unity's JsonUtility.ToJson()
          const parsed = payload ? JSON.parse(payload) : {}
          handler(parsed)
        } catch {
          // If payload isn't valid JSON, pass it raw
          handler(payload)
        }
      } else {
        console.warn(`[useUnityBridge] No handler for action: ${action}`)
      }
    }

    window.addEventListener('unityEvent', handleUnityEvent)
    return () => window.removeEventListener('unityEvent', handleUnityEvent)
  }, [handlers])

  // ── Send React → Unity ─────────────────────────────────────────────────
  const sendToUnity = useCallback((action, payload = {}) => {
    if (window._unityBridge?.send) {
      window._unityBridge.send(action, payload)
    } else {
      // Running outside WebView — log for development
      console.log(`[useUnityBridge DEV] → Unity: ${action}`, payload)
    }
  }, [])

  return sendToUnity
}
