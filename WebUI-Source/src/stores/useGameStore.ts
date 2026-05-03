import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'

// Types
interface InventoryItem {
  instanceId: string
  id: number
  name: string
  material: 'plastic' | 'metal' | 'paper'
  color: string
  emoji: string
  displayName: string
  rawClass?: string
  confidence?: number
  isRecyclable?: boolean
}

interface BotParts {
  head?: InventoryItem
  body?: InventoryItem
  arm_l?: InventoryItem
  arm_r?: InventoryItem
  leg_l?: InventoryItem
  leg_r?: InventoryItem
}

interface BotStats {
  hp: number
  def: number
  spd: number
  attack: number
  elementalBonus: string
}

interface Bot {
  id: string
  parts: BotParts
  stats: BotStats
  createdAt: number
}

// Material base stats from GDD
const MATERIAL_BASE: Record<'plastic' | 'metal' | 'paper', { hp: number; def: number; spd: number; weakness: string }> = {
  plastic: { hp: 50, def: 30, spd: 80, weakness: 'heat' },
  metal: { hp: 80, def: 90, spd: 40, weakness: 'electrical' },
  paper: { hp: 40, def: 20, spd: 100, weakness: 'fire' }
}

// Part multipliers from GDD
const PART_MULTIPLIER = {
  head: { hp: 0.3, def: 0.3, spd: 0.3 },
  body: { hp: 2.0, def: 1.5, spd: 0.5 },
  arm_l: { hp: 1.0, def: 1.0, spd: 1.0 },
  arm_r: { hp: 1.0, def: 1.0, spd: 1.0 },
  leg_l: { hp: 0.5, def: 0.5, spd: 2.5 },
  leg_r: { hp: 0.5, def: 0.5, spd: 2.5 }
}

// Calculate bot stats based on parts
function calculateBotStats(parts: BotParts): BotStats {
  let totalHp = 0, totalDef = 0, totalSpd = 0;
  
  (Object.keys(parts) as Array<keyof BotParts>).forEach((slot) => {
    const item = parts[slot];
    if (!item) return;
    const material = item.material as 'plastic' | 'metal' | 'paper';
    const base = MATERIAL_BASE[material];
    const mult = PART_MULTIPLIER[slot as keyof typeof PART_MULTIPLIER];
    totalHp += base.hp * mult.hp;
    totalDef += base.def * mult.def;
    totalSpd += base.spd * mult.spd;
  });
  
  // Apply completion bonus
  const slotCount = Object.keys(parts).filter(k => parts[k as keyof BotParts]).length
  const completionBonus = slotCount * 10
  
  const finalHp = Math.round(totalHp + completionBonus)
  const finalDef = Math.round(totalDef + completionBonus)
  const finalSpd = Math.round(totalSpd + completionBonus)
  
  // Determine elemental bonus
  const materialCount: Record<'plastic' | 'metal' | 'paper', number> = { plastic: 0, metal: 0, paper: 0 }
  Object.values(parts).forEach(p => {
    if (p?.material) {
      const mat = p.material as 'plastic' | 'metal' | 'paper'
      materialCount[mat]++
    }
  })
  const dominant = Object.entries(materialCount).sort((a, b) => b[1] - a[1])[0]
  
  return {
    hp: finalHp,
    def: finalDef,
    spd: finalSpd,
    attack: Math.round(finalHp * 0.5 + finalDef * 0.3),
    elementalBonus: dominant[0] || 'plastic'
  }
}

export const ROBOT_LIBRARY = [
  {
    id: 'bot_lvl1',
    name: 'SCRAP-O-TRON',
    level: 1,
    description: 'A basic but reliable robot made from reinforced paper and plastic.',
    recipe: { plastic: 2, metal: 2, paper: 1 },
    stats: { hp: 120, def: 80, spd: 150, attack: 60 },
    prerequisiteId: null
  },
  {
    id: 'bot_lvl2',
    name: 'IRON-GEAR PRIME',
    level: 2,
    description: 'A heavy-duty combat machine with specialized metal shielding.',
    recipe: { plastic: 5, metal: 10, paper: 2 },
    stats: { hp: 250, def: 200, spd: 40, attack: 120 },
    prerequisiteId: 'bot_lvl1'
  }
]

interface GameStore {
  // Resources
  scrap: number
  volt: number
  materials: { plastic: number; metal: number; paper: number }
  
  // Progression
  unlockedRobotIds: string[]
  activeBotId: string | null
  inventory: InventoryItem[]
  xp: number
  playerLevel: number
  totalScrap: number
  
  // Actions
  addMaterial: (type: 'plastic' | 'metal' | 'paper', amount: number) => void
  addScrap: (amount: number) => void
  addVolt: (amount: number) => void
  buildRobot: (robotId: string) => boolean
  addToInventory: (item: InventoryItem) => void
  addXp: (amount: number) => void
  setActiveBotId: (id: string | null) => void
  resetGame: () => void
  importSampleData: () => void
}

export const useGameStore = create<GameStore>()(
  persist(
    (set, get) => ({
      // Initial State (Zeroed for first-time user)
      scrap: 0,
      volt: 0,
      materials: { plastic: 0, metal: 0, paper: 0 },
      unlockedRobotIds: [],
      activeBotId: null,
      inventory: [],
      xp: 0,
      playerLevel: 1,
      totalScrap: 0,

      // Actions
      addMaterial: (type, amount) => set(state => ({
        materials: { ...state.materials, [type]: state.materials[type] + amount }
      })),
      
      addScrap: (amount) => set(state => ({ scrap: state.scrap + amount })),
      
      addVolt: (amount) => set(state => ({ volt: state.volt + amount })),
      
      addToInventory: (item) => set(state => ({
        inventory: [...state.inventory, item]
      })),

      addXp: (amount) => set(state => {
        const newXp = state.xp + amount
        const nextLevelThreshold = state.playerLevel * 100
        if (newXp >= nextLevelThreshold) {
          return { xp: newXp - nextLevelThreshold, playerLevel: state.playerLevel + 1 }
        }
        return { xp: newXp }
      }),

      setActiveBotId: (id) => set({ activeBotId: id }),
      
      buildRobot: (robotId) => {
        const robot = ROBOT_LIBRARY.find(r => r.id === robotId)
        if (!robot) return false

        const { materials, unlockedRobotIds } = get()
        
        // Check prerequisite
        if (robot.prerequisiteId && !unlockedRobotIds.includes(robot.prerequisiteId)) {
          return false
        }

        // Check materials
        if (
          materials.plastic >= robot.recipe.plastic &&
          materials.metal >= robot.recipe.metal &&
          materials.paper >= robot.recipe.paper
        ) {
          set((state) => ({
            materials: {
              plastic: state.materials.plastic - robot.recipe.plastic,
              metal: state.materials.metal - robot.recipe.metal,
              paper: state.materials.paper - robot.recipe.paper
            },
            unlockedRobotIds: [...state.unlockedRobotIds, robotId],
            activeBotId: robotId
          }))
          return true
        }
        return false
      },

      resetGame: () => set({
        scrap: 0,
        volt: 0,
        materials: { plastic: 0, metal: 0, paper: 0 },
        unlockedRobotIds: [],
        activeBotId: null,
        inventory: []
      }),

      importSampleData: () => set({
        scrap: 500,
        volt: 100,
        materials: { plastic: 50, metal: 30, paper: 20 },
        unlockedRobotIds: ['bot_lvl1'],
        activeBotId: 'bot_lvl1',
        inventory: [
          { instanceId: 's1', id: 101, name: 'SODA_CAN', material: 'metal', color: '#888', emoji: '🥫', displayName: 'Soda Can', isRecyclable: true }
        ]
      })
    }),
    {
      name: 'buildabot-storage',
      storage: createJSONStorage(() => localStorage),
    }
  )
)

// Helper to get material color
export function getMaterialColor(material: 'plastic' | 'metal' | 'paper'): string {
  const colors = { plastic: '#007AFF', metal: '#FF9500', paper: '#34C759' }
  return colors[material]
}

// Helper to get material emoji
export function getMaterialEmoji(material: 'plastic' | 'metal' | 'paper'): string {
  const emojis = { plastic: '🍶', metal: '🥫', paper: '📄' }
  return emojis[material]
}