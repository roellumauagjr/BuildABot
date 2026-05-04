import React, { useState, useCallback, useMemo, useEffect, useRef } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { Icon } from './Icons'
import { useUnityBridge, ACTIONS } from '../hooks/useUnityBridge'
import { useGameStore, ROBOT_LIBRARY } from '../stores/useGameStore'
import { COLORS, SHADOW } from '../constants/theme'
import RobotPreview from './RobotPreview'

interface BattleScreenProps {
  onBack: () => void
}

export default function BattleScreen({ onBack }: BattleScreenProps) {
  // Phase state: 'landing' shows the info/start, 'ar' shows the live feed + drawer
  const [phase, setPhase] = useState<'landing' | 'ar'>('landing')
  const [selectedBotId, setSelectedBotId] = useState<string | null>(null)
  
  const { activeBotId, unlockedRobotIds } = useGameStore()
  
  const activeBot = useMemo(() => 
    ROBOT_LIBRARY.find(r => r.id === activeBotId), 
    [activeBotId]
  )

  const sendToUnity = useUnityBridge()

  const handleStartBattle = () => {
    setPhase('ar')
    // Default to 'robot1' (index 0) if no active/unlocked bot is set.
    // null botId causes ARBotController.SetPendingBot to receive an empty string
    // which leaves spawnOptionIndex unchanged or at a bad default.
    const initialBotId = (activeBotId && unlockedRobotIds.includes(activeBotId))
      ? activeBotId
      : 'robot1'
    setSelectedBotId(initialBotId)
    
    // Explicitly tell Unity to start the AR sequence with the selected bot
    sendToUnity(ACTIONS.INITIATE_AR, { botId: initialBotId })
  }

  const handleBackToLanding = () => {
    setPhase('landing')
    sendToUnity(ACTIONS.DESPAWN_BOT, {})
  }

  const handleExitBattle = () => {
    sendToUnity(ACTIONS.DESPAWN_BOT, {})
    onBack()
  }

  const selectRobot = (botId: string) => {
    if (!unlockedRobotIds.includes(botId)) return
    setSelectedBotId(botId)
    // Notify Unity which bot is currently "in hand" for placement
    sendToUnity('SELECT_BOT', { botId })
  }

  // Camera management is handled by Unity AR on this page.
  // The WebUI is transparent to allow the Unity feed to show through.

  return (
    <div className="battle-page-container" style={{ 
      position: 'absolute', 
      inset: 0, 
      backgroundColor: phase === 'landing' ? COLORS.background : 'transparent',
      overflow: 'hidden',
      zIndex: 100
    }}>
      <AnimatePresence mode="wait">
        {phase === 'landing' ? (
          <motion.div
            key="landing"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0, scale: 1.1 }}
            transition={{ duration: 0.5 }}
            style={{
              height: '100%',
              display: 'flex',
              flexDirection: 'column',
              padding: 24,
              justifyContent: 'center',
              alignItems: 'center',
              textAlign: 'center',
              background: `radial-gradient(circle at center, ${COLORS.primary}10 0%, ${COLORS.background} 100%)`
            }}
          >
            <motion.div
              initial={{ scale: 0.8, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              transition={{ delay: 0.2, type: 'spring' }}
              style={{
                width: 120,
                height: 120,
                borderRadius: 40,
                backgroundColor: COLORS.backgroundSecondary,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                marginBottom: 24,
                boxShadow: SHADOW.lg
              }}
            >
              <Icon name="shield" size={60} color={COLORS.primary} />
            </motion.div>

            <motion.h1
              initial={{ y: 20, opacity: 0 }}
              animate={{ y: 0, opacity: 1 }}
              transition={{ delay: 0.3 }}
              style={{ fontSize: 32, fontWeight: 800, color: COLORS.textPrimary, marginBottom: 12 }}
            >
              COMBAT ZONE
            </motion.h1>

            <motion.p
              initial={{ y: 20, opacity: 0 }}
              animate={{ y: 0, opacity: 1 }}
              transition={{ delay: 0.4 }}
              style={{ fontSize: 16, color: COLORS.gray1, marginBottom: 40, maxWidth: 280 }}
            >
              Deploy your robots into the real world and prepare for combat maneuvers.
            </motion.p>

            <motion.div
              initial={{ y: 20, opacity: 0 }}
              animate={{ y: 0, opacity: 1 }}
              transition={{ delay: 0.5 }}
              style={{ width: '100%', maxWidth: 300, gap: 12, display: 'flex', flexDirection: 'column' }}
            >
              <button
                onClick={handleStartBattle}
                style={{
                  width: '100%',
                  padding: '20px',
                  borderRadius: 20,
                  backgroundColor: COLORS.red,
                  color: '#fff',
                  border: 'none',
                  fontSize: 18,
                  fontWeight: 700,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 12,
                  boxShadow: '0 8px 24px rgba(255, 59, 48, 0.3)'
                }}
              >
                <Icon name="bolt" size={20} color="#fff" />
                ENTER AR ARENA
              </button>

              <button
                onClick={handleExitBattle}
                style={{
                  width: '100%',
                  padding: '16px',
                  borderRadius: 18,
                  backgroundColor: 'transparent',
                  color: COLORS.gray1,
                  border: `2px solid ${COLORS.gray4}`,
                  fontSize: 16,
                  fontWeight: 600
                }}
              >
                RETURN TO HUB
              </button>
            </motion.div>
          </motion.div>
        ) : (
          <motion.div
            key="ar-hud"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="ar-hud-mode"
            style={{ 
              height: '100%', 
              position: 'relative', 
              backgroundColor: 'transparent', 
              pointerEvents: 'none' 
            }}
          >
            {/* Unity AR feed shows through here via transparency */}
            <div style={{ 
              position: 'absolute', 
              inset: 0, 
              zIndex: 0, 
              backgroundColor: 'transparent', 
              pointerEvents: 'none' 
            }}>
            </div>
            {/* Top Controls */}
            <div style={{
              position: 'absolute',
              top: 50,
              left: 20,
              right: 20,
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              zIndex: 100,
              pointerEvents: 'auto' // Re-enable clicks for buttons
            }}>
              <motion.button
                initial={{ x: -20, opacity: 0 }}
                animate={{ x: 0, opacity: 1 }}
                onClick={handleBackToLanding}
                style={{
                  width: 44,
                  height: 44,
                  borderRadius: 14,
                  backgroundColor: 'rgba(0,0,0,0.5)',
                  backdropFilter: 'blur(10px)',
                  border: '1px solid rgba(255,255,255,0.2)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  cursor: 'pointer'
                }}
              >
                <Icon name="arrow-left" size={24} color="#fff" />
              </motion.button>

              <div style={{
                padding: '8px 16px',
                borderRadius: 20,
                backgroundColor: 'rgba(0,0,0,0.5)',
                backdropFilter: 'blur(10px)',
                border: '1px solid rgba(255,255,255,0.2)',
                color: '#fff',
                fontSize: 12,
                fontWeight: 600,
                letterSpacing: 1,
                display: 'flex',
                alignItems: 'center',
                gap: 8
              }}>
                <motion.div 
                  animate={{ opacity: [1, 0.4, 1] }}
                  transition={{ repeat: Infinity, duration: 1.5 }}
                  style={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: COLORS.primary }} 
                />
                AR FEED ACTIVE
              </div>
            </div>


            {/* Bottom Robot Drawer */}
            <motion.div
              initial={{ y: 200 }}
              animate={{ y: 0 }}
              transition={{ type: 'spring', damping: 25, stiffness: 200, delay: 0.2 }}
              style={{
                position: 'absolute',
                bottom: 0,
                left: 0,
                right: 0,
                backgroundColor: 'rgba(0,0,0,0.85)',
                backdropFilter: 'blur(20px)',
                borderTop: '1px solid rgba(255,255,255,0.1)',
                padding: '24px 20px 44px',
                zIndex: 100,
                boxShadow: '0 -10px 40px rgba(0,0,0,0.5)',
                pointerEvents: 'auto' // Re-enable clicks for drawer
              }}
            >
              <div style={{ 
                color: 'rgba(255,255,255,0.5)', 
                fontSize: 11, 
                fontWeight: 800, 
                letterSpacing: 1.5, 
                marginBottom: 16,
                textTransform: 'uppercase',
                textAlign: 'center'
              }}>
                Deployment Bay
              </div>

              <div style={{
                display: 'flex',
                gap: 16,
                overflowX: 'auto',
                paddingBottom: 8,
                scrollbarWidth: 'none',
                WebkitOverflowScrolling: 'touch'
              }}>
                {ROBOT_LIBRARY.map((robot) => {
                  const unlocked = unlockedRobotIds.includes(robot.id)
                  const isActive = selectedBotId === robot.id
                  
                  return (
                    <motion.button
                      key={robot.id}
                      whileTap={{ scale: 0.95 }}
                      onClick={() => selectRobot(robot.id)}
                      style={{
                        flex: '0 0 140px',
                        height: 160,
                        borderRadius: 28,
                        backgroundColor: isActive ? COLORS.primary + '40' : 'rgba(255,255,255,0.08)',
                        border: `2px solid ${isActive ? COLORS.primary : 'rgba(255,255,255,0.15)'}`,
                        display: 'flex',
                        flexDirection: 'column',
                        alignItems: 'center',
                        justifyContent: 'center',
                        padding: 10,
                        opacity: unlocked ? 1 : 0.4,
                        cursor: unlocked ? 'pointer' : 'not-allowed',
                        position: 'relative',
                        overflow: 'hidden',
                        transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)',
                        boxShadow: isActive ? `0 0 20px ${COLORS.primary}40` : 'none'
                      }}
                    >
                      <div style={{ width: '100%', height: 110, marginBottom: 4 }}>
                        <RobotPreview 
                          robotId={robot.id} 
                          isUnlocked={unlocked} 
                          active={isActive}
                        />
                      </div>
                      
                      <div style={{ 
                        fontSize: 11, 
                        color: '#fff', 
                        fontWeight: 700, 
                        textAlign: 'center',
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        width: '100%'
                      }}>
                        {robot.name}
                      </div>

                      {!unlocked && (
                        <div style={{
                          position: 'absolute',
                          inset: 0,
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          backgroundColor: 'rgba(0,0,0,0.3)'
                        }}>
                          <Icon name="lock" size={20} color="rgba(255,255,255,0.7)" />
                        </div>
                      )}
                      
                      {isActive && (
                        <motion.div 
                          layoutId="activeGlow"
                          style={{
                            position: 'absolute',
                            bottom: 0,
                            left: 0,
                            right: 0,
                            height: 4,
                            backgroundColor: COLORS.primary,
                            boxShadow: `0 0 10px ${COLORS.primary}`
                          }}
                        />
                      )}
                    </motion.button>
                  )
                })}
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}