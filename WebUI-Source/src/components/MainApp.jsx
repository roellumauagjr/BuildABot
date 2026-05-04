import { useState, useEffect, useCallback, useMemo } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { Icon, SimpleIcon } from './Icons'
import { COLORS } from '../constants/theme'
import RobotPreview from './RobotPreview'
import { useUnityBridge, ACTIONS } from '../hooks/useUnityBridge'

import { useGameStore, getMaterialColor, ROBOT_LIBRARY } from '../stores/useGameStore'
import { RADIUS, SPACING, TYPOGRAPHY, SHADOW } from '../constants/theme'

import ScannerScreen from './ScannerScreen'
import BuildScreen from './BuildScreen'
import BattleScreen from './BattleScreen'

// Throttle function to limit event frequency
function throttle(func, limit) {
  let inThrottle
  return function(...args) {
    if (!inThrottle) {
      func.apply(this, args)
      inThrottle = true
      setTimeout(() => inThrottle = false, limit)
    }
  }
}

export default function MainApp() {
  const [activeScreen, setActiveScreen] = useState('hub')
  const [showSettings, setShowSettings] = useState(false)
  const [gyroData, setGyroData] = useState({ x: 0, y: 0 })
  
  const { scrap, volt, materials, unlockedRobotIds, activeBotId } = useGameStore()
  const activeBot = useMemo(() => {
    return ROBOT_LIBRARY.find(r => r.id === activeBotId)
  }, [activeBotId])
  
  // Set AR active attribute on body for transparency CSS overrides
  useEffect(() => {
    const isAR = activeScreen === 'battle' || activeScreen === 'scan'
    document.body.setAttribute('data-ar-active', isAR.toString())
    return () => document.body.removeAttribute('data-ar-active')
  }, [activeScreen])

  // Throttled gyro update - only 10 times per second max
  useEffect(() => {
    const throttledHandler = throttle((e) => {
      if (e.gamma !== null && e.beta !== null) {
        setGyroData({
          x: Math.max(-1, Math.min(1, e.gamma / 30)),
          y: Math.max(-1, Math.min(1, (e.beta - 45) / 30))
        })
      }
    }, 100) // 100ms = 10fps max
    
    if (typeof window !== 'undefined' && window.DeviceOrientationEvent) {
      window.addEventListener('deviceorientation', throttledHandler, { passive: true })
    }
    return () => {
      if (typeof window !== 'undefined') {
        try { window.removeEventListener('deviceorientation', throttledHandler) } catch (e) {}
      }
    }
  }, [])
  
  const sendToUnity = useUnityBridge()
  
  // Send initial page on mount
  useEffect(() => {
    sendToUnity(ACTIONS.SET_PAGE, { page: 'hub' })
  }, [sendToUnity])

  const navigate = (screen) => {
    setActiveScreen(screen)
    sendToUnity(ACTIONS.SET_PAGE, { page: screen })
  }
  
  const tabs = [
    { id: 'hub', label: 'Home', icon: 'home' },
    { id: 'scan', label: 'Scan', icon: 'camera' },
    { id: 'forge', label: 'Forge', icon: 'hammer' },
    { id: 'battle', label: 'Battle', icon: 'shield' }
  ]
  
  const renderScreen = () => {
    switch (activeScreen) {
      case 'scan':
        return <ScannerScreenWrapper key="scan" onBack={() => setActiveScreen('hub')} />
      case 'forge':
        return <BuildScreenWrapper key="forge" onBack={() => setActiveScreen('hub')} />
      case 'battle':
        return <BattleScreenWrapper key="battle" onBack={() => setActiveScreen('hub')} />
      default:
        return <HubContent key="hub" gyroData={gyroData} onNavigate={setActiveScreen} activeBot={activeBot} onOpenSettings={() => setShowSettings(true)} />
    }
  }
  
  return (
    <div className="app-container" style={{
      width: '100%',
      height: '100%',
      backgroundColor: (activeScreen === 'battle' || activeScreen === 'scan') ? 'transparent' : COLORS.background,
      overflow: 'hidden',
      display: 'flex',
      flexDirection: 'column',
      position: 'relative',
      fontFamily: "'Google Sans', -apple-system, sans-serif"
    }}>
      {/* Main Content Area */}
      <div className="app-content" style={{ flex: 1, overflow: 'hidden', position: 'relative' }}>
        <AnimatePresence mode="wait">
          <motion.div
            key={activeScreen}
            initial={{ opacity: 0, x: activeScreen === 'hub' ? 0 : 20 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: activeScreen === 'hub' ? 0 : -20 }}
            transition={{ type: 'spring', stiffness: 300, damping: 30 }}
            style={{ width: '100%', height: '100%' }}
          >
            {renderScreen()}
          </motion.div>
        </AnimatePresence>
      </div>
      
      {/* Bottom Navigation - Only visible on Hub for immersion */}
      <AnimatePresence>
        {activeScreen === 'hub' && (
          <BottomNavigation 
            tabs={tabs} 
            activeTab={activeScreen} 
            onTabChange={setActiveScreen}
          />
        )}
      </AnimatePresence>

      {/* Settings Modal */}
      <AnimatePresence>
        {showSettings && (
          <SettingsModal 
            onClose={() => setShowSettings(false)} 
            onReset={() => {
              useGameStore.getState().resetGame()
              setShowSettings(false)
              setActiveScreen('hub')
            }}
            onImportSample={() => {
              useGameStore.getState().importSampleData()
              setShowSettings(false)
            }}
          />
        )}
      </AnimatePresence>
    </div>
  )
}

// Settings Modal Component
function SettingsModal({ onClose, onReset, onImportSample }) {
  const [confirmReset, setConfirmReset] = useState(false)

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0,0,0,0.85)',
        backdropFilter: 'blur(10px)',
        zIndex: 2000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 20
      }}
      onClick={onClose}
    >
      <motion.div
        initial={{ scale: 0.9, opacity: 0, y: 20 }}
        animate={{ scale: 1, opacity: 1, y: 0 }}
        exit={{ scale: 0.9, opacity: 0, y: 20 }}
        onClick={(e) => e.stopPropagation()}
        style={{
          width: '100%',
          maxWidth: 340,
          backgroundColor: '#1c1c1e',
          borderRadius: 32,
          padding: 24,
          border: '1px solid rgba(255,255,255,0.1)',
          boxShadow: '0 20px 40px rgba(0,0,0,0.4)'
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
          <h2 style={{ margin: 0, fontSize: 22, fontWeight: 700, color: '#fff' }}>Settings</h2>
          <motion.button 
            whileTap={{ scale: 0.9 }}
            onClick={onClose}
            style={{ width: 36, height: 36, borderRadius: 18, backgroundColor: 'rgba(255,255,255,0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}
          >
            <Icon name="close" size={18} color="#fff" />
          </motion.button>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div style={{ padding: 16, backgroundColor: 'rgba(255,255,255,0.03)', borderRadius: 20, border: '1px solid rgba(255,255,255,0.05)' }}>
            <div style={{ fontSize: 13, color: 'rgba(255,255,255,0.4)', marginBottom: 8, fontWeight: 600, letterSpacing: 0.5 }}>GAME DATA</div>
            
            {!confirmReset ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                <motion.button
                  whileTap={{ scale: 0.98 }}
                  onClick={onImportSample}
                  style={{
                    width: '100%',
                    padding: '14px',
                    borderRadius: 12,
                    backgroundColor: 'rgba(0, 122, 255, 0.1)',
                    color: '#007AFF',
                    fontSize: 15,
                    fontWeight: 600,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 10
                  }}
                >
                  <Icon name="plus" size={18} color="#007AFF" />
                  Add Sample Data
                </motion.button>

                <motion.button
                  whileTap={{ scale: 0.98 }}
                  onClick={() => setConfirmReset(true)}
                  style={{
                    width: '100%',
                    padding: '14px',
                    borderRadius: 12,
                    backgroundColor: 'rgba(255, 59, 48, 0.1)',
                    color: '#FF3B30',
                    fontSize: 15,
                    fontWeight: 600,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 10
                  }}
                >
                  <Icon name="trash" size={18} color="#FF3B30" />
                  Reset All Progress
                </motion.button>
              </div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <p style={{ margin: 0, fontSize: 13, color: '#FF3B30', textAlign: 'center', fontWeight: 500 }}>
                  This will delete all bots, scrap, and materials. Are you sure?
                </p>
                <div style={{ display: 'flex', gap: 10 }}>
                  <button 
                    onClick={() => setConfirmReset(false)}
                    style={{ flex: 1, padding: 12, borderRadius: 10, backgroundColor: 'rgba(255,255,255,0.1)', color: '#fff', fontSize: 14, fontWeight: 600 }}
                  >
                    Cancel
                  </button>
                  <button 
                    onClick={onReset}
                    style={{ flex: 1, padding: 12, borderRadius: 10, backgroundColor: '#FF3B30', color: '#fff', fontSize: 14, fontWeight: 600 }}
                  >
                    Confirm Reset
                  </button>
                </div>
              </div>
            )}
          </div>

          <div style={{ padding: 16, backgroundColor: 'rgba(255,255,255,0.03)', borderRadius: 20, border: '1px solid rgba(255,255,255,0.05)' }}>
            <div style={{ fontSize: 13, color: 'rgba(255,255,255,0.4)', marginBottom: 12, fontWeight: 600, letterSpacing: 0.5 }}>ABOUT</div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14, color: 'rgba(255,255,255,0.7)' }}>
              <span>Version</span>
              <span style={{ fontWeight: 600 }}>1.0.4-PREVIEW</span>
            </div>
          </div>
        </div>

        <p style={{ textAlign: 'center', fontSize: 11, color: 'rgba(255,255,255,0.2)', marginTop: 24, fontWeight: 500 }}>
          BUILD A BOT PROTOCOL &copy; 2026
        </p>
      </motion.div>
    </motion.div>
  )
}

// Bottom Navigation Component
function BottomNavigation({ tabs, activeTab, onTabChange }) {
  return (
    <motion.div
      initial={{ y: 100, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      exit={{ y: 100, opacity: 0 }}
      transition={{ type: 'spring', stiffness: 400, damping: 40 }}
      style={{
        position: 'absolute',
        bottom: 20,
        left: 16,
        right: 16,
        backgroundColor: 'rgba(255,255,255,0.95)',
        backdropFilter: 'blur(20px)',
        borderRadius: 28,
        padding: '12px 8px',
        display: 'flex',
        justifyContent: 'space-around',
        boxShadow: '0 8px 32px rgba(0,0,0,0.12), 0 2px 8px rgba(0,0,0,0.08)',
        border: '1px solid rgba(0,0,0,0.05)',
        zIndex: 1000
      }}
    >
      {tabs.map((tab) => {
        const isActive = activeTab === tab.id
        
        return (
          <motion.button
            key={tab.id}
            onClick={() => onTabChange(tab.id)}
            whileTap={{ scale: 0.85 }}
            whileHover={{ scale: 1.05 }}
            style={{
              flex: 1,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              padding: '8px 12px',
              background: isActive ? `${COLORS.primary}15` : 'transparent',
              borderRadius: 20,
              border: 'none',
              cursor: 'pointer',
              position: 'relative',
              gap: 4
            }}
          >
            <AnimatePresence>
              {isActive && (
                <motion.div
                  layoutId="navIndicator"
                  style={{
                    position: 'absolute',
                    top: -8,
                    width: 24,
                    height: 3,
                    backgroundColor: COLORS.primary,
                    borderRadius: 3
                  }}
                />
              )}
            </AnimatePresence>
            
            <motion.div
              animate={{
                scale: isActive ? 1.15 : 1,
                color: isActive ? COLORS.primary : COLORS.gray2
              }}
              transition={{ type: 'spring', stiffness: 500, damping: 30 }}
            >
              <Icon name={tab.icon} size={isActive ? 22 : 20} color={isActive ? COLORS.primary : COLORS.gray2} />
            </motion.div>
            
            <motion.span
              animate={{
                fontSize: isActive ? 11 : 10,
                fontWeight: isActive ? 600 : 400,
                color: isActive ? COLORS.primary : COLORS.gray2
              }}
              transition={{ type: 'spring', stiffness: 500, damping: 30 }}
            >
              {tab.label}
            </motion.span>
          </motion.button>
        )
      })}
    </motion.div>
  )
}

// Hub Content
function HubContent({ gyroData, onNavigate, activeBot, onOpenSettings }) {
  const { scrap, volt, materials, unlockedRobotIds } = useGameStore()
  
  // Get screen width for responsiveness
  const screenWidth = typeof window !== 'undefined' ? window.innerWidth : 375
  const isSmallScreen = screenWidth < 360
  const isLargeScreen = screenWidth > 400
  
  const paddingHorizontal = Math.max(16, screenWidth * 0.04)
  const headerFontSize = isSmallScreen ? 26 : isLargeScreen ? 38 : 34
  const cardPadding = isSmallScreen ? 12 : 20
  
  return (
    <div style={{
      width: '100%',
      height: '100%',
      overflow: 'hidden',
      position: 'relative',
      paddingBottom: 80,
      background: COLORS.background
    }}>
      {/* Subtle header accent only */}
      <div
        style={{
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          height: 200,
          background: `linear-gradient(180deg, ${COLORS.primary}08 0%, transparent 100%)`,
          pointerEvents: 'none'
        }}
      />
      
      {/* Header */}
      <motion.div
        initial={{ y: -30, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        transition={{ delay: 0.1 }}
        style={{
          padding: `52px ${paddingHorizontal}px 16px`,
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start'
        }}
      >
        <div>
          <motion.h1
            initial={{ y: -10, opacity: 0 }}
            animate={{ y: 0, opacity: 1 }}
            transition={{ delay: 0.15 }}
            style={{
              fontSize: headerFontSize,
              fontWeight: 700,
              color: COLORS.textPrimary,
              marginBottom: 4,
              letterSpacing: -0.5
            }}
          >
            Build A Bot
          </motion.h1>
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.2 }}
            style={{ display: 'flex', alignItems: 'center', gap: 8 }}
          >
            <motion.span
              initial={{ scale: 0.8 }}
              animate={{ scale: 1 }}
              style={{
                backgroundColor: COLORS.primary + '15',
                color: COLORS.primary,
                padding: '4px 10px',
                borderRadius: 20,
                fontSize: 12,
                fontWeight: 600
              }}
            >
              System Rank {unlockedRobotIds.length}
            </motion.span>
            <span style={{ fontSize: 12, color: COLORS.gray1 }}>
              {unlockedRobotIds.length} bot{unlockedRobotIds.length !== 1 ? 's' : ''} unlocked
            </span>
          </motion.div>
        </div>
        
        <motion.button
          whileTap={{ scale: 0.9 }}
          onClick={onOpenSettings}
          style={{
            width: 44,
            height: 44,
            borderRadius: 14,
            backgroundColor: COLORS.backgroundSecondary,
            border: 'none',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer'
          }}
        >
          <Icon name="settings" size={20} color={COLORS.gray1} />
        </motion.button>
      </motion.div>
      
      
      {/* Active Bot Card */}
      <div style={{ padding: `0 ${paddingHorizontal}px`, marginBottom: 16 }}>
        <motion.div
          initial={{ y: 20, opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          transition={{ delay: 0.25 }}
          style={{
            backgroundColor: COLORS.background,
            borderRadius: 24,
            padding: cardPadding,
            boxShadow: SHADOW.md
          }}
        >
          <div style={{ 
            display: 'flex', 
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: 12
          }}>
            <span style={{ fontSize: 17, fontWeight: 600 }}>Active Bot</span>
            {activeBot && (
              <motion.span
                initial={{ scale: 0.8 }}
                animate={{ scale: 1 }}
                style={{
                  fontSize: 11,
                  fontWeight: 600,
                  color: COLORS.secondary,
                  backgroundColor: COLORS.secondary + '15',
                  padding: '4px 10px',
                  borderRadius: 20,
                  textTransform: 'uppercase'
                }}
              >
                LEVEL {activeBot.level}
              </motion.span>
            )}
          </div>
          
          {activeBot ? (
            <>
              <motion.div
                initial={{ scale: 0.9 }}
                animate={{ scale: 1 }}
                style={{
                  height: 130,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  backgroundColor: COLORS.backgroundSecondary,
                  borderRadius: 16,
                  marginBottom: 12,
                  overflow: 'hidden'
                }}
              >
                <RobotPreview robotId={activeBot.id} isUnlocked={true} />
              </motion.div>
              
              <div style={{
                display: 'flex',
                justifyContent: 'space-around',
                paddingTop: 12,
                borderTop: `1px solid ${COLORS.gray5}`
              }}>
                <StatItem label="HP" value={activeBot.stats.hp} color={COLORS.secondary} delay={0.3} />
                <StatItem label="DEF" value={activeBot.stats.def} color={COLORS.primary} delay={0.35} />
                <StatItem label="SPD" value={activeBot.stats.spd} color={COLORS.tertiary} delay={0.4} />
                <StatItem label="ATK" value={activeBot.stats.attack} color={COLORS.red} delay={0.45} />
              </div>
            </>
          ) : (
            <div style={{
              height: 130,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              backgroundColor: COLORS.backgroundSecondary,
              borderRadius: 16,
              gap: 8
            }}>
              <Icon name="flask" size={32} color={COLORS.gray3} />
              <span style={{ fontSize: 14, color: COLORS.gray1 }}>
                No bot assembled yet
              </span>
              <span style={{ fontSize: 12, color: COLORS.gray2 }}>
                Go to Forge to create one
              </span>
            </div>
          )}
        </motion.div>
      </div>
      
      {/* Materials Section */}
      <div style={{ padding: `0 ${paddingHorizontal}px`, marginBottom: 20 }}>
        <motion.div
          initial={{ y: 20, opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          transition={{ delay: 0.35 }}
          style={{ display: 'flex', gap: 10 }}
        >
          {[
            { type: 'plastic', color: COLORS.plastic },
            { type: 'metal', color: COLORS.metal },
            { type: 'paper', color: COLORS.paper },
          ].map((mat, i) => (
            <motion.div
              key={mat.type}
              initial={{ y: 20, opacity: 0 }}
              animate={{ y: 0, opacity: 1 }}
              transition={{ delay: 0.4 + i * 0.05 }}
              whileHover={{ scale: 1.02, y: -2 }}
              style={{
                flex: 1,
                backgroundColor: mat.color + '12',
                borderRadius: 16,
                padding: 12,
                textAlign: 'center',
                border: `1px solid ${mat.color}25`
              }}
            >
              <SimpleIcon type={mat.type} size="medium" color={mat.color} />
              <div style={{ 
                fontSize: 11, 
                color: mat.color, 
                textTransform: 'capitalize',
                marginTop: 4,
                marginBottom: 2 
              }}>
                {mat.type}
              </div>
              <div style={{ 
                fontSize: 20, 
                fontWeight: 700, 
                color: mat.color 
              }}>
                {materials[mat.type]}
              </div>
            </motion.div>
          ))}
        </motion.div>
      </div>
      
      {/* Battle Button */}
      <div style={{ padding: `0 ${paddingHorizontal}px` }}>
        <motion.button
          initial={{ y: 20, opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          transition={{ delay: 0.5 }}
          whileTap={{ scale: 0.96 }}
          onClick={() => {
            if (!activeBot) {
              alert('Please assemble a bot in the Forge first!')
              return
            }
            onNavigate('battle')
          }}
          disabled={!activeBot}
          style={{
            width: '100%',
            padding: 16,
            borderRadius: 16,
            border: 'none',
            backgroundColor: activeBot ? COLORS.red : COLORS.gray4,
            color: '#fff',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 10,
            fontSize: 17,
            fontWeight: 600,
            cursor: activeBot ? 'pointer' : 'not-allowed',
            boxShadow: activeBot ? '0 8px 24px rgba(255,59,48,0.35)' : 'none'
          }}
        >
          <Icon name="shield" size={20} color="#fff" />
          {activeBot ? 'Enter Battle Arena' : 'Build a Bot First'}
        </motion.button>
      </div>
    </div>
  )
}

// Currency Card Component
function CurrencyCard({ icon, label, value, color, delay }) {
  return (
    <motion.div
      initial={{ x: -20, opacity: 0 }}
      animate={{ x: 0, opacity: 1 }}
      transition={{ delay }}
      whileTap={{ scale: 0.96 }}
      style={{
        flex: 1,
        backgroundColor: COLORS.background,
        borderRadius: 16,
        padding: 14,
        display: 'flex',
        alignItems: 'center',
        gap: 12,
        boxShadow: SHADOW.sm
      }}
    >
      <div style={{
        width: 40,
        height: 40,
        borderRadius: 12,
        backgroundColor: color + '15',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center'
      }}>
        <Icon name={icon} size={20} color={color} />
      </div>
      <div>
        <div style={{ fontSize: 11, color: COLORS.gray1 }}>{label}</div>
        <div style={{ fontSize: 18, fontWeight: 700 }}>{value}</div>
      </div>
    </motion.div>
  )
}

// Stat Item Component
function StatItem({ label, value, color, delay }) {
  return (
    <motion.div
      initial={{ scale: 0.8, opacity: 0 }}
      animate={{ scale: 1, opacity: 1 }}
      transition={{ delay }}
      style={{ textAlign: 'center' }}
    >
      <div style={{ fontSize: 11, color: COLORS.gray1, marginBottom: 2 }}>{label}</div>
      <div style={{ fontSize: 18, fontWeight: 700, color }}>{value}</div>
    </motion.div>
  )
}

// Screen Wrappers
function ScannerScreenWrapper({ onBack }) {
  const { addMaterial, addToInventory, addScrap } = useGameStore()

  const handleCapture = (item) => {
    if (!item || !item.material || !item.isRecyclable) return

    const mat = item.material  // 'plastic' | 'metal' | 'paper'

    // +1 to the material count (single call — ScannerScreen no longer calls this)
    addMaterial(mat, 1)

    // +5–10 scrap reward
    addScrap(Math.floor(Math.random() * 6) + 5)

    // Full InventoryItem — provides all fields the BuildScreen expects
    const colorMap   = { plastic: '#007AFF', metal: '#FF9500', paper: '#34C759' }
    const emojiMap   = { plastic: '🍶',      metal: '🥫',      paper: '📄'      }
    const nameMap    = { plastic: 'PLASTIC',  metal: 'METAL',   paper: 'PAPER'   }
    addToInventory({
      instanceId:  `scan_${Date.now()}_${Math.random().toString(36).slice(2)}`,
      id:          Date.now(),
      name:        nameMap[mat] ?? mat.toUpperCase(),
      material:    mat,
      color:       colorMap[mat] ?? '#888',
      emoji:       emojiMap[mat] ?? '📦',
      displayName: item.displayName ?? mat,
      rawClass:    item.rawClass   ?? '',
      confidence:  item.confidence ?? 1,
      isRecyclable: true,
    })
  }

  return <ScannerScreen onCapture={handleCapture} onBack={onBack} />
}

function BuildScreenWrapper({ onBack }) {
  const inventory = useGameStore(state => state.inventory)
  return <BuildScreen inventory={inventory} onBack={onBack} />
}

function BattleScreenWrapper({ onBack }) {
  return <BattleScreen onBack={onBack} />
}