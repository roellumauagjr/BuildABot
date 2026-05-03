import React, { useState, useEffect } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { Icon } from './Icons'
import { useGameStore, ROBOT_LIBRARY } from '../stores/useGameStore'
import { COLORS } from '../constants/theme'
import RobotPreview from './RobotPreview'

export default function BuildScreen({ onBack }) {
  const { materials, unlockedRobotIds, buildRobot, scrap } = useGameStore()
  const [activeIndex, setActiveIndex] = useState(0)
  const [showRecipe, setShowRecipe] = useState(null) // ID of robot to show recipe for

  const currentRobot = ROBOT_LIBRARY[activeIndex]
  const isUnlocked = unlockedRobotIds.includes(currentRobot.id)
  const isPrereqMet = !currentRobot.prerequisiteId || unlockedRobotIds.includes(currentRobot.prerequisiteId)

  const handleUnlock = () => {
    if (!isPrereqMet) return
    const success = buildRobot(currentRobot.id)
    if (success) {
      setShowRecipe(null)
    }
  }

  return (
    <div className="forge-screen">
      {/* Top Nav */}
      <div className="forge-header">
        <button className="back-btn" onClick={onBack}>
          <Icon name="arrow-left" size={24} />
        </button>
        <div className="resource-chips">
          <div className={`mini-mat-chip plastic ${materials.plastic >= currentRobot.recipe.plastic ? 'met' : ''}`}>
            <Icon name="plastic" size={12} color="#007AFF" />
            <span>{materials.plastic} / {currentRobot.recipe.plastic}</span>
          </div>
          <div className={`mini-mat-chip metal ${materials.metal >= currentRobot.recipe.metal ? 'met' : ''}`}>
            <Icon name="metal" size={12} color="#FF9500" />
            <span>{materials.metal} / {currentRobot.recipe.metal}</span>
          </div>
          <div className={`mini-mat-chip paper ${materials.paper >= currentRobot.recipe.paper ? 'met' : ''}`}>
            <Icon name="paper" size={12} color="#34C759" />
            <span>{materials.paper} / {currentRobot.recipe.paper}</span>
          </div>
        </div>
      </div>

      {/* Carousel Area */}
      <div className="forge-carousel">
        <div className="carousel-track" style={{ transform: `translateX(-${activeIndex * 100}%)` }}>
          {ROBOT_LIBRARY.map((robot, idx) => {
            const unlocked = unlockedRobotIds.includes(robot.id)
            
            return (
              <div key={robot.id} className="carousel-item">
                <div className={`robot-display ${unlocked ? 'is-active' : 'is-locked'}`}>
                  <div className="robot-silhouette">
                    <RobotPreview 
                      robotId={robot.id} 
                      isUnlocked={unlocked} 
                    />
                    
                    {!unlocked && (
                      <div className="lock-overlay">
                        <Icon name="lock" size={60} color="rgba(255,255,255,0.2)" />
                      </div>
                    )}
                  </div>
                  
                  <div className="robot-info">
                    <h2>{robot.name}</h2>
                    <div className="stat-pills">
                      <div className="pill">ATK {robot.stats.attack}</div>
                      <div className="pill">HP {robot.stats.hp}</div>
                      <div className="pill">SPD {robot.stats.spd}</div>
                    </div>
                  </div>
                </div>
              </div>
            )
          })}
        </div>

        {/* Navigation Arrows */}
        {activeIndex > 0 && (
          <button className="nav-btn prev" onClick={() => setActiveIndex(a => a - 1)}>
            <Icon name="chevronLeft" size={24} />
          </button>
        )}
        {activeIndex < ROBOT_LIBRARY.length - 1 && (
          <button className="nav-btn next" onClick={() => setActiveIndex(a => a + 1)}>
            <Icon name="chevronRight" size={24} />
          </button>
        )}
      </div>

      {/* Action Area */}
      <div className="forge-actions">
        {isUnlocked ? (
          <div className="unlocked-msg">
            <Icon name="check" size={20} color="#34C759" />
            <span>ROBOT READY FOR DEPLOYMENT</span>
            <button className="deploy-btn" onClick={onBack}>DEPLOY TO HUB</button>
          </div>
        ) : (
          <div className="lock-state">
            {!isPrereqMet ? (
              <div className="prereq-warning">
                <Icon name="info" size={18} />
                <p>Requires {ROBOT_LIBRARY.find(r => r.id === currentRobot.prerequisiteId)?.name} first</p>
              </div>
            ) : (
              <button className="unlock-trigger" onClick={() => setShowRecipe(currentRobot.id)}>
                <Icon name="bolt" size={20} />
                UNLOCK LEVEL {currentRobot.level}
              </button>
            )}
          </div>
        )}
      </div>

      {/* Recipe Modal */}
      <AnimatePresence>
        {showRecipe && (
          <motion.div 
            className="recipe-modal-overlay"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <motion.div 
              className="recipe-card"
              initial={{ scale: 0.9, y: 20 }}
              animate={{ scale: 1, y: 0 }}
            >
              <h3>FORGE RECIPE</h3>
              <p className="recipe-desc">{currentRobot.description}</p>
              
              <div className="materials-grid">
                {Object.entries(currentRobot.recipe).map(([mat, count]) => {
                  const hasEnough = materials[mat] >= count
                  return (
                    <div key={mat} className={`mat-item ${hasEnough ? 'met' : 'missing'}`}>
                      <div className="mat-icon">
                        <Icon name={mat} size={24} />
                      </div>
                      <div className="mat-count">
                        <span className="current">{materials[mat]}</span>
                        <span className="target">/{count}</span>
                      </div>
                      <div className="mat-label">{mat.toUpperCase()}</div>
                    </div>
                  )
                })}
              </div>

              <div className="recipe-footer">
                <button className="close-btn" onClick={() => setShowRecipe(null)}>CANCEL</button>
                <button 
                  className="build-btn" 
                  disabled={!Object.entries(currentRobot.recipe).every(([m, c]) => materials[m] >= c)}
                  onClick={handleUnlock}
                >
                  ASSEMBLE UNIT
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      <style>{`
        .forge-screen {
          position: fixed;
          inset: 0;
          background: #000;
          color: #fff;
          display: flex;
          flex-direction: column;
          overflow: hidden;
          font-family: 'Inter', sans-serif;
          z-index: 100;
        }
        .forge-header {
          padding: 20px;
          display: flex;
          align-items: center;
          gap: 15px;
          background: linear-gradient(to bottom, rgba(255,255,255,0.05), transparent);
        }
        .back-btn {
          background: rgba(255,255,255,0.1);
          border: 1px solid rgba(255,255,255,0.1);
          width: 44px;
          height: 44px;
          border-radius: 12px;
          color: #fff;
          display: flex;
          align-items: center;
          justify-content: center;
        }
        .resource-chips {
          margin-left: auto;
          display: flex;
          gap: 6px;
          flex-shrink: 0;
        }
        .mini-mat-chip {
          background: rgba(255,255,255,0.05);
          border: 1px solid rgba(255,255,255,0.1);
          padding: 6px 10px;
          border-radius: 10px;
          display: flex;
          align-items: center;
          gap: 6px;
          font-weight: 800;
          font-size: 11px;
          transition: all 0.3s ease;
          white-space: nowrap;
          flex-shrink: 0;
        }
        .mini-mat-chip.plastic { border-color: #007AFF40; background: #007AFF10; }
        .mini-mat-chip.metal { border-color: #FF950040; background: #FF950010; }
        .mini-mat-chip.paper { border-color: #34C75940; background: #34C75910; }
        .mini-mat-chip.met { border-color: #fff; background: rgba(255,255,255,0.2); }

        @media (max-width: 360px) {
          .mini-mat-chip { padding: 4px 8px; font-size: 10px; gap: 4px; }
          .forge-header { padding: 12px; gap: 8px; }
        }

        .forge-carousel {
          flex: 1;
          position: relative;
          display: flex;
          align-items: center;
          padding: 40px 0;
        }
        .carousel-track {
          display: flex;
          width: 100%;
          height: 100%;
          transition: transform 0.5s cubic-bezier(0.23, 1, 0.32, 1);
        }
        .carousel-item {
          min-width: 100%;
          height: 100%;
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          padding: 20px;
        }

        .robot-display {
          width: 100%;
          max-width: 320px;
          text-align: center;
        }
        .robot-silhouette {
          width: 240px;
          height: 320px;
          margin: 0 auto 30px;
          position: relative;
          display: flex;
          align-items: center;
          justify-content: center;
        }
        .placeholder-3d {
          width: 100%;
          height: 100%;
          background: rgba(255,255,255,0.03);
          border-radius: 40px;
          display: flex;
          align-items: center;
          justify-content: center;
          border: 1px solid rgba(255,255,255,0.05);
          position: relative;
        }
        .placeholder-3d.active {
          background: transparent;
          border-color: rgba(255,149,0,0.3);
        }
        .model-aperture {
          width: 100%;
          height: 100%;
          background: transparent;
        }
        .lock-overlay {
          position: absolute;
          inset: 0;
          display: flex;
          align-items: center;
          justify-content: center;
          pointer-events: none;
        }
        .bot-glow {
          position: absolute;
          inset: 40px;
          background: ${COLORS.orange};
          filter: blur(60px);
          opacity: 0.15;
          border-radius: 50%;
          animation: breathe 4s infinite;
        }
        @keyframes breathe {
          0%, 100% { opacity: 0.1; transform: scale(1); }
          50% { opacity: 0.2; transform: scale(1.1); }
        }

        .robot-info h2 {
          font-size: 28px;
          font-weight: 900;
          margin: 0 0 15px;
          letter-spacing: -1px;
        }
        .stat-pills {
          display: flex;
          justify-content: center;
          gap: 10px;
        }
        .pill {
          background: rgba(255,255,255,0.08);
          padding: 6px 12px;
          border-radius: 8px;
          font-size: 11px;
          font-weight: 700;
          letter-spacing: 0.5px;
          color: rgba(255,255,255,0.7);
        }

        .nav-btn {
          position: absolute;
          top: 50%;
          transform: translateY(-50%);
          background: rgba(255,255,255,0.05);
          border: none;
          width: 50px;
          height: 80px;
          color: #fff;
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 10;
        }
        .nav-btn.prev { left: 0; border-radius: 0 20px 20px 0; }
        .nav-btn.next { right: 0; border-radius: 20px 0 0 20px; }

        .forge-actions {
          padding: 40px 20px;
          background: linear-gradient(to top, rgba(255,149,0,0.1), transparent);
        }
        .unlock-trigger {
          width: 100%;
          background: ${COLORS.orange};
          height: 56px;
          background: ${COLORS.primary};
          color: #fff;
          border: none;
          border-radius: 16px;
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 10px;
          font-weight: 800;
          font-size: 16px;
          text-transform: uppercase;
          letter-spacing: 1px;
          box-shadow: 0 10px 25px ${COLORS.primary}40;
        }
        .prereq-warning {
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          gap: 10px;
          color: ${COLORS.error};
          font-size: 13px;
          font-weight: 600;
          background: ${COLORS.error}10;
          padding: 16px 20px;
          border-radius: 12px;
          border: 1px solid ${COLORS.error}30;
          text-align: center;
        }
        .unlocked-msg {
          text-align: center;
          display: flex;
          flex-direction: column;
          align-items: center;
          gap: 15px;
        }
        .unlocked-msg span {
          font-size: 12px;
          font-weight: 700;
          letter-spacing: 1px;
          color: #34C759;
        }
        .deploy-btn {
          width: 100%;
          background: #fff;
          border: none;
          height: 54px;
          border-radius: 14px;
          color: #000;
          font-weight: 800;
        }

        .recipe-modal-overlay {
          position: fixed;
          inset: 0;
          background: rgba(0,0,0,0.9);
          z-index: 200;
          display: flex;
          align-items: center;
          justify-content: center;
          padding: 20px;
          backdrop-filter: blur(10px);
        }
        .recipe-card {
          background: #111;
          border: 1px solid rgba(255,255,255,0.1);
          width: 100%;
          max-width: 400px;
          border-radius: 30px;
          padding: 30px;
        }
        .recipe-card h3 {
          margin: 0 0 10px;
          font-weight: 900;
          letter-spacing: 2px;
          color: ${COLORS.orange};
        }
        .recipe-desc {
          font-size: 14px;
          opacity: 0.6;
          line-height: 1.5;
          margin-bottom: 30px;
        }
        .materials-grid {
          display: grid;
          grid-template-columns: repeat(3, 1fr);
          gap: 15px;
          margin-bottom: 40px;
        }
        .mat-item {
          background: rgba(255,255,255,0.03);
          border: 1px solid rgba(255,255,255,0.05);
          border-radius: 20px;
          padding: 15px 10px;
          text-align: center;
        }
        .mat-item.met { border-color: rgba(52,199,89,0.3); background: rgba(52,199,89,0.05); }
        .mat-item.missing { border-color: rgba(255,59,48,0.3); }
        
        .mat-icon { margin-bottom: 10px; opacity: 0.8; }
        .mat-count { font-weight: 800; font-size: 16px; margin-bottom: 4px; }
        .missing .current { color: #FF3B30; }
        .met .current { color: #34C759; }
        .mat-label { font-size: 10px; font-weight: 800; opacity: 0.4; }

        .recipe-footer {
          display: flex;
          gap: 12px;
        }
        .recipe-footer button {
          flex: 1;
          height: 50px;
          border-radius: 12px;
          font-weight: 800;
          font-size: 14px;
        }
        .close-btn { background: transparent; border: 1px solid rgba(255,255,255,0.1); color: #fff; }
        .build-btn { background: #fff; border: none; color: #000; }
        .build-btn:disabled { opacity: 0.2; }
      `}</style>
    </div>
  )
}
