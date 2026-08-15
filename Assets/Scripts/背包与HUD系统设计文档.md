# 背包系统 & HUD 系统 —— 完整设计文档

## 目录

- [一、总览与架构](#一总览与架构)
- [二、场景搭建指南](#二场景搭建指南)
- [三、背包系统](#三背包系统)
  - [3.1 数据层 API](#31-数据层-api)
  - [3.2 主面板 Inspector 字段](#32-主面板-inspector-字段)
  - [3.3 预制体结构](#33-预制体结构)
  - [3.4 交互流程详述](#34-交互流程详述)
  - [3.5 手柄焦点导航](#35-手柄焦点导航)
- [四、HUD 系统](#四hud-系统)
  - [4.1 HUDManager](#41-hudmanager)
  - [4.2 PlayerStatusHUD（左上状态栏）](#42-playerstatushud左上状态栏)
  - [4.3 SkillBarHUD（右上技能栏）](#43-skillbarhud右上技能栏)
  - [4.4 QuickItemBarHUD（右下快捷栏）](#44-quickitembarhud右下快捷栏)
  - [4.5 EnemyHealthBarHUD（锁定/Boss 血条）](#45-enemyhealthbarhud锁定boss-血条)
  - [4.6 FloatingTextManager（浮动数字）](#46-floatingtextmanager浮动数字)
  - [4.7 InteractionPromptHUD（交互提示）](#47-interactionprompthud交互提示)
  - [4.8 ItemObtainToastHUD（拾取弹窗）](#48-itemobtaintoasthud拾取弹窗)
- [五、背包 UI 面板](#五背包-ui-面板)
  - [5.1 InventoryPanel（主面板）](#51-inventorypanel主面板)
  - [5.2 InventoryItemUI（物品格子）](#52-inventoryitemui物品格子)
  - [5.3 EquipmentPanel（装备栏）](#53-equipmentpanel装备栏)
  - [5.4 ItemDetailPanel（物品详情）](#54-itemdetailpanel物品详情)
  - [5.5 QuickSlotConfigPanel（快捷栏配置）](#55-quickslotconfigpanel快捷栏配置)
- [六、跨平台操作映射](#六跨平台操作映射)
- [七、美术资源需求清单](#七美术资源需求清单)
- [八、代码调用指南](#八代码调用指南)
- [九、与旧系统共存策略](#九与旧系统共存策略)

---

## 一、总览与架构

### 数据流

```
                  ┌─────────────┐
                  │   ItemSO    │  (旧 ScriptableObject，不修改)
                  │  .id .name  │
                  │  .icon .itemType │
                  └──────┬──────┘
                         │ ItemAdapter 适配
                         ▼
              ┌─────────────────────┐
              │  IInventoryItem     │  接口（InventoryInterfaces.cs）
              │  IEquipment         │
              │  IConsumable        │
              │  ISpirit / ITreasure│
              └──────┬──────────────┘
                     │
    ┌────────────────┼────────────────┐
    ▼                ▼                ▼
InventoryService  EquipmentManager  QuickSlotManager
    │                │                │
    └────────────────┼────────────────┘
                     │ InventoryEvents 静态事件
                     ▼
    ┌────────────────┼────────────────┐
    ▼                ▼                ▼
 [背包 UI]       [HUD 面板]      [外部系统]
 InventoryPanel  PlayerStatusHUD  AudioManager
 EquipmentPanel  SkillBarHUD      (订阅事件播放音效)
 ItemDetailPanel QuickItemBarHUD
                 EnemyHealthBarHUD
```

**核心原则**：数据层（左列）永远不引用 UI 层。UI 通过订阅 `InventoryEvents` 被动更新。

---

## 二、场景搭建指南

### 步骤 1：创建 Persistent 对象

```
Hierarchy:
  Persistent (GameObject, 标记 DontDestroyOnLoad)
  ├── InventoryService      (脚本)
  ├── EquipmentManager      (脚本)
  └── QuickSlotManager      (脚本)
```

- `InventoryService`：Inspector 设置 `Max Capacity = 99`
- `EquipmentManager`：无需 Inspector 配置
- `QuickSlotManager`：无需 Inspector 配置

### 步骤 2：创建 HUD Canvas

```
Hierarchy:
  HUD_Canvas (Canvas, ScreenSpaceOverlay, SortOrder=10)
  ├── CanvasScaler (1920x1080, ScaleWithScreenSize, Match=0.5)
  ├── GraphicRaycaster
  └── HUDManager (脚本)
      ├── PlayerStatusHUD       # 挂到左上角子对象
      ├── SkillBarHUD           # 挂到右上角子对象
      ├── QuickItemBarHUD       # 挂到右下角子对象
      ├── EnemyHealthBarHUD     # 挂到中上子对象(Boss) + 独立Canvas(世界空间锁定)
      ├── FloatingTextManager   # 挂到全屏覆盖子对象
      ├── InteractionPromptHUD  # 挂到中央子对象
      └── ItemObtainToastHUD    # 挂到右侧子对象
```

### 步骤 3：创建背包 Canvas

```
Hierarchy:
  Inventory_Canvas (Canvas, ScreenSpaceOverlay, SortOrder=20)
  ├── CanvasScaler (1920x1080, ScaleWithScreenSize, Match=0.5)
  ├── GraphicRaycaster
  └── InventoryPanel (脚本, 初始 SetActive=false)
      ├── CategoryTabs (HorizontalLayoutGroup)
      ├── LeftPanel ─── EquipmentPanel (脚本)
      ├── CenterPanel
      │   ├── SortBar (Dropdown + Toggle)
      │   └── ItemGrid (ScrollRect > Viewport > Content, GridLayoutGroup)
      ├── RightPanel ── ItemDetailPanel (脚本)
      └── BottomPanel ─ QuickSlotConfigPanel (脚本)
```

---

## 三、背包系统

### 3.1 数据层 API

#### InventoryService（单例 `InventoryService.Instance`）

```csharp
// 增删
int  AddItem(ItemSO item, int count = 1);  // 返回实际放入数量，自动堆叠
int  RemoveItem(string itemId, int count); // 返回实际移除数量
bool UseItem(string itemId);               // 消耗品-1，返回是否成功

// 查询
int  GetItemCount(string itemId);          // 某物品总持有数
bool HasItem(string itemId, int count);    // 是否持有足够数量
List<ItemSlot> GetFilteredItems();          // 按当前分类/排序返回

// 排序筛选（由 UI 设置）
ItemCategory ActiveCategory { get; set; }  // 默认 All
SortMode ActiveSortMode { get; set; }      // 默认 AcquisitionTime
bool SortDescending { get; set; }          // 默认 true

// 新物品标记
void MarkAllAsViewed();                     // 关闭背包时调用

// 容量
int MaxCapacity { get; }                   // 默认 99
int CurrentCount { get; }                  // 当前槽位数
bool IsFull { get; }                       // 是否满
```

#### EquipmentManager（单例 `EquipmentManager.Instance`）

```csharp
ItemSO Equip(IEquipment item);                    // 装备，返回被换下的旧装备
ItemSO Unequip(EquipmentSlotType slotType);       // 卸下，返回卸下的装备

IEquipment GetEquipment(EquipmentSlotType slot);  // 查某槽位
bool IsEquipped(string itemId);                   // 是否已装备

List<EquipmentEffect> GetTotalEffects();          // 汇总所有装备属性
List<EquipmentEffect> CompareEquipment(EquipmentSlotType slot, IEquipment newItem); // 对比差异
List<SetBonus> ActiveSetBonuses { get; }          // 当前激活的套装效果
```

#### QuickSlotManager（单例 `QuickSlotManager.Instance`）

```csharp
void AssignSlot(int index, ItemSO item);  // 放入槽位 0-3
void ClearSlot(int index);                // 清空槽位
bool UseSlot(int index);                  // 使用1个，用尽自动清空

ItemSO GetSlotItem(int index);            // 查询
int GetSlotCount(int index);              // 查询剩余数量
```

#### InventoryEvents（静态事件，`InventoryEvents.OnXxx += handler`）

| 事件 | 签名 | 触发时机 |
|------|------|---------|
| `OnItemAdded` | `(ItemSO, int count)` | 新槽位创建 |
| `OnItemRemoved` | `(ItemSO, int remaining)` | 槽位归零 |
| `OnItemCountChanged` | `(ItemSO, int newCount)` | 堆叠数变化 |
| `OnEquipmentChanged` | `(EquipmentSlotType, ItemSO)` | 装备上 |
| `OnEquipmentRemoved` | `(EquipmentSlotType, ItemSO)` | 卸下 |
| `OnSetBonusChanged` | `()` | 套装效果变化 |
| `OnQuickSlotChanged` | `(int slotIndex, ItemSO)` | 快捷栏变化 |
| `OnQuickSlotCountChanged` | `(int slotIndex, int count)` | 快捷栏数量变化 |
| `OnSpiritEquipped` | `(ItemSO)` | 精魄装备 |
| `OnTreasureEquipped` | `(ItemSO)` | 法宝装备 |
| `OnNewItemObtained` | `(ItemSO)` | 首次获得某物品 |
| `OnCapacityChanged` | `(int current, int max)` | 容量变化 |

---

## 四、HUD 系统

### 4.1 HUDManager

**挂载位置**：HUD_Canvas 根对象。

**Inspector 字段**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `_currentMode` | `HUDMode` 枚举 | 初始模式，默认 Exploration |
| `_statusHUD` | `PlayerStatusHUD` | 拖入左上状态栏组件（可选，自动 GetComponentInChildren） |
| `_quickItemBar` | `QuickItemBarHUD` | 拖入右下快捷栏组件 |
| `_skillBar` | `SkillBarHUD` | 拖入右上技能栏组件 |
| `_enemyHealthBar` | `EnemyHealthBarHUD` | 拖入血条组件 |
| `_floatingText` | `FloatingTextManager` | 拖入浮动数字组件 |
| `_interactionPrompt` | `InteractionPromptHUD` | 拖入交互提示组件 |
| `_itemToast` | `ItemObtainToastHUD` | 拖入拾取弹窗组件 |

**模式切换 API**：

```csharp
HUDManager.Instance.SetMode(HUDMode.Combat);  // 进入战斗
HUDManager.Instance.SetMode(HUDMode.Boss);    // Boss 战
HUDManager.Instance.SetMode(HUDMode.Dialogue); // 对话中
HUDManager.Instance.SetMode(HUDMode.Cutscene); // 播片中
```

**模式行为表**：

| 模式 | StatusBar | SkillBar | QuickItem | BossBar | 其余面板 |
|------|-----------|----------|-----------|---------|----------|
| Exploration | 显示 | **隐藏** | 显示 | 隐藏 | interaction提示可见 |
| Combat | 显示 | 显示 | 显示 | 隐藏 | 锁定血条可见 |
| Boss | 显示 | 显示 | 显示 | **显示** | 锁定血条隐藏 |
| Dialogue | alpha=0.3 | 隐藏 | alpha=0.3 | 隐藏 | 对话 UI 独占 |
| Cutscene | **alpha=0** | **alpha=0** | **alpha=0** | **alpha=0** | 全部不可见 |

---

### 4.2 PlayerStatusHUD（左上状态栏）

**锚点**：左上角，偏移 (40, -40)。占屏幕约 280×200。

**Inspector 字段**：

| 字段 | 类型 | 说明 | 美术需求 |
|------|------|------|----------|
| `_hpFill` | `Image` | 血条填充，fillMethod=Horizontal, fillOrigin=Left | 红色渐变条，宽220高18 |
| `_hpText` | `Text` | 血量数字 "100/100" | 白色24号字体，对齐右 |
| `_mpFill` | `Image` | 法力条填充 | 蓝色渐变条，宽220高14 |
| `_mpText` | `Text` | 法力数字 | 白色20号字体 |
| `_spFill` | `Image` | 耐力条填充 | 黄色渐变条，宽220高14 |
| `_spText` | `Text` | 耐力数字 | 白色20号字体 |
| `_chargeDots` | `Image[4]` | 棍势点圆形指示器 | 直径24圆形，白色(未满)/金色(满蓄) |
| `_flashDuration` | `float` | 受伤闪烁时长 | 默认 0.15 |
| `_flashColor` | `Color` | 闪烁颜色 | 默认红色半透明 |
| `_lerpSpeed` | `float` | 平滑过渡速度 | 默认 5 |

**布局结构**：

```
PlayerStatusHUD (RectTransform: 左上锚点, 280×200)
├── LevelText (Text, 左上 "Lv.XX", 金色24号)
├── HP_Bar (Image bg 灰色底, 220×18)
│   ├── HP_Fill (Image 红色, fillAmount=HpRatio)
│   └── HP_Text (Text "100/100", 右上对齐)
├── MP_Bar (Image bg 灰色底, 220×14)
│   ├── MP_Fill (Image 蓝色, fillAmount=MpRatio)
│   └── MP_Text (Text)
├── SP_Bar (Image bg 灰色底, 220×14)
│   ├── SP_Fill (Image 黄色, fillAmount=SpRatio)
│   └── SP_Text (Text)
└── ChargeDots (HorizontalLayoutGroup, 间距4)
    ├── Dot0 (Image 圆形24×24)
    ├── Dot1 (Image 圆形24×24)
    ├── Dot2 (Image 圆形24×24)
    └── Dot3 (Image 圆形24×24)
```

**状态展示**：

| 状态 | 血量条 | 法力条 | 耐力条 | 棍势点 |
|------|--------|--------|--------|--------|
| 满 | 红色100%填充 | 蓝色100%填充 | 黄色100%填充 | 白色圆点全亮 |
| 消耗 | Lerp平滑递减(5x/s) | 直接递减 | 直接递减 | 逐个暗灭(从左到右) |
| 受伤 | 触发闪烁(0.15s红白交替) | - | - | - |
| 不足 | - | 灰色填充 | 灰色+闪烁 | - |
| 满蓄 | - | - | - | 4段满→**金色高亮** |

**代码更新入口**：

```csharp
// 由 PlayerProperty 或其他系统每帧或事件驱动
PlayerStatusHUD hud = GetComponent<PlayerStatusHUD>();
hud.SetHP(currentHP, maxHP);
hud.SetMP(currentMP, maxMP);
hud.SetSP(currentSP, maxSP);
hud.SetChargePoints(currentCharge);  // 0-4
```

---

### 4.3 SkillBarHUD（右上技能栏）

**锚点**：右上角，偏移 (-40, -40)。占屏幕约 120×200。

**Inspector 字段**：

| 字段 | 类型 | 说明 | 美术需求 |
|------|------|------|----------|
| `_spiritSlot.Icon` | `Image` | 精魄技能图标 | 64×64 方形图标 |
| `_spiritSlot.CooldownOverlay` | `Image` | 冷却遮罩 | 灰色半透明，fillMethod=Top, fillOrigin=Top |
| `_spiritSlot.CooldownText` | `Text` | 冷却倒数秒数 | 白色32号粗体，居中 |
| `_spiritSlot.ReadyGlow` | `GameObject` | 就绪发光 | 金色边框/光晕 |
| `_treasureSlot.*` | (同上) | 法宝技能 | 64×64 + 遮罩 + 文字 + 光晕 |

**布局结构**：

```
SkillBarHUD (RectTransform: 右上锚点, 120×200)
├── SpiritSlot (64×64, 间距8)
│   ├── Icon (Image, 彩色完整图标)
│   ├── CooldownOverlay (Image, fillMethod=Top, 初始 fillAmount=0)
│   ├── CooldownText (Text, "30", 居中)
│   └── ReadyGlow (GameObject, 初始隐藏)
└── TreasureSlot (64×64, 间距8)
    └── (同上结构)
```

**状态展示**：

| 状态 | 图标 | 遮罩 | 文字 | 光晕 |
|------|------|------|------|------|
| 未装备 | 灰暗/空白 | 隐藏 | 隐藏 | 隐藏 |
| 就绪 | 彩色完整 | fillAmount=0 | 隐藏 | **显示**金色光晕 |
| 冷却中 | 彩色(底层) | fillAmount=remaining/total 从顶覆盖 | `Ceil(remaining)` | 隐藏 |
| 冷却结束 | 彩色完整 | 隐藏(瞬间) | 隐藏 | **显示** |

**代码更新入口**：

```csharp
SkillBarHUD hud = GetComponent<SkillBarHUD>();
hud.SetSpiritSkill(iconSprite, 30f);   // 图标 + 冷却30秒
hud.SetTreasureSkill(iconSprite, 45f);
hud.StartSpiritCooldown();  // 使用后开始冷却
```

---

### 4.4 QuickItemBarHUD（右下快捷栏）

**锚点**：右下角，偏移 (-60, 60)。占屏幕约 200×200。

**布局**：十字排列——上(↑)、下(↓)、左(←)、右(→)。

```
QuickItemBarHUD (RectTransform: 右下锚点)
├── Slot_Up (64×64, 位置: 中上)
│   ├── IconBg (Image, 圆形底色)
│   ├── Icon (Image, 物品图标)
│   ├── CountText (Text, "x5", 右下角16号)
│   ├── KeyHint (Text, "↑", 上方12号)
│   └── EmptyState (GameObject, 空槽占位图)
├── Slot_Down (64×64) ── 同上
├── Slot_Left (64×64) ── 同上
└── Slot_Right (64×64) ── 同上
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `_slots[0-3]` | `QuickSlotUI` 结构体数组 | 顺序: 0=↑, 1=↓, 2=←, 3=→ |
| 每个 `QuickSlotUI.Icon` | `Image` | 物品图标 48×48 |
| 每个 `QuickSlotUI.IconBg` | `Image` | 背景底图 64×64（槽位0金色边框=葫芦） |
| 每个 `QuickSlotUI.CountText` | `Text` | 剩余数量，如 "5/8" |
| 每个 `QuickSlotUI.KeyHint` | `Text` | 方向按键提示 |
| 每个 `QuickSlotUI.EmptyState` | `GameObject` | 空槽显示 "—" 或空白 |

**关键逻辑**：`Start()` 中订阅 `InventoryEvents.OnQuickSlotChanged` / `OnQuickSlotCountChanged`，无需手动更新。

---

### 4.5 EnemyHealthBarHUD（锁定/Boss 血条）

**分两部分**：Boss 血条（屏幕中上，ScreenSpace） + 锁定血条（世界空间跟随）。

#### Boss 血条

**锚点**：顶部居中，偏移 (0, -20)。宽 600×80。

| 字段 | 类型 | 说明 |
|------|------|------|
| `_bossBarRoot` | `GameObject` | 整个 Boss 血条父对象 |
| `_bossNameText` | `Text` | Boss 名称，白色36号 |
| `_bossHpFill` | `Image` | 血条填充，红色渐变 560×24 |
| `_bossHpPercent` | `Text` | 百分比 "75%" |
| `_bossStanceIcon` | `Image` | 架势/霸体状态图标 32×32 |

```
BossBarRoot (顶部居中, 初始隐藏)
├── BossNameText ("黑风大王")
├── HpBarBg (Image 灰色底 560×24)
│   └── BossHpFill (Image 红色, fillAmount=Lerp)
├── HpPercentText ("75%")
└── StanceIcon (32×32, 可选)
```

**API**：

```csharp
enemyBar.ShowBossBar("黑风大王", 1.0f);     // 显示, 满血
enemyBar.UpdateBossHP(0.75f);                // 扣到75%
enemyBar.HideBossBar();                      // Boss 死/脱战
```

#### 普通锁定血条

独立 Canvas（`RenderMode=WorldSpace`，Scale=0.01），挂在锁定目标头顶 2.5m 处。

| 字段 | 类型 | 说明 |
|------|------|------|
| `_targetBarCanvas` | `Canvas` | 世界空间 Canvas |
| `_targetHpFill` | `Image` | 锁定血条填充 200×12 |
| `_targetNameText` | `Text` | 敌人名称 18号 |

**API**：

```csharp
enemyBar.ShowTargetBar(enemyTransform, "妖怪", 1.0f);
enemyBar.UpdateTargetHP(0.5f);
enemyBar.HideTargetBar();
```

---

### 4.6 FloatingTextManager（浮动数字）

**挂载**：HUD_Canvas 上，覆盖全屏的 Panel。

| 字段 | 类型 | 说明 |
|------|------|------|
| `_damagePrefab` | `FloatingText` | 伤害数字预制体（白色/黄色暴击） |
| `_healPrefab` | `FloatingText` | 治疗数字预制体（绿色+号） |
| `_critPrefab` | `FloatingText` | 暴击数字预制体（黄色放大） |
| `_floatSpeed` | `float` | 上飘速度，默认 1.5 |
| `_fadeDuration` | `float` | 淡出时长，默认 1.2s |
| `_randomOffset` | `float` | 随机偏移范围，默认 0.4 |

**FloatingText 预制体结构**：

```
FloatingText (RectTransform, 100×40)
├── Text (白色32号，带 Outline 组件描边)
└── (空，纯文字)
```

对象池：每种 10 个初始 + 30 个上限。超出不显示。

**API**：

```csharp
FloatingTextManager.Instance.ShowDamage(enemyPos, 85, isCrit: false);   // "85" 白色
FloatingTextManager.Instance.ShowDamage(enemyPos, 200, isCrit: true);   // "200" 黄色放大
FloatingTextManager.Instance.ShowHeal(playerPos, 30);                    // "+30" 绿色
```

---

### 4.7 InteractionPromptHUD（交互提示）

**锚点**：底部居中，偏移 (0, 120)。占屏幕约 200×60。

| 字段 | 类型 | 说明 |
|------|------|------|
| `_canvasGroup` | `CanvasGroup` | 控制淡入淡出 |
| `_keyIcon` | `Image` | 按键图标 32×32（E键图标） |
| `_promptText` | `Text` | 提示文字 "拾取" 24号白色 |
| `_fadeSpeed` | `float` | 淡入淡出速度，默认 8 |

```
InteractionPromptHUD (底部居中)
├── KeyIcon (Image 32×32, E键图标)
└── PromptText (Text "拾取物品")
```

**API**：

```csharp
prompt.Show(keyIconSprite, "拾取物品");
prompt.Hide();  // 玩家离开范围
```

---

### 4.8 ItemObtainToastHUD（拾取弹窗）

**锚点**：右侧居中偏上，偏移 (-20, 80)。占屏幕约 200×320。

| 字段 | 类型 | 说明 |
|------|------|------|
| `_slots[0-3]` | `ToastSlot` 数组 | 4 个弹窗槽位 |
| `_displayDuration` | `float` | 每条停留时间，默认 2.5s |
| `_exitSlideSpeed` | `float` | 淡出速度，默认 2x |

**ToastSlot 结构体**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Root` | `GameObject` | 槽位根对象 |
| `Icon` | `Image` | 物品图标 40×40 |
| `HighlightBorder` | `Image` | 新物品高亮边框（金色） |
| `NameText` | `Text` | 物品名称 20号 |
| `CountText` | `Text` | 数量 "x1" |
| `CanvasGroup` | `CanvasGroup` | 淡出控制 |

**排队逻辑**：4 个槽位轮转。满时新物品进队列等待。每条停留 2.5s 后 CanvasGroup.alpha 在 0.5s 内淡到 0，释放槽位。

自动订阅 `InventoryEvents.OnNewItemObtained`，无需手动调用。

---

## 五、背包 UI 面板

### 5.1 InventoryPanel（主面板）

**锚点**：全屏居中，850×700（约占屏幕 85%），初始 `SetActive(false)`。

| 字段 | 类型 | 说明 |
|------|------|------|
| `_panelRoot` | `GameObject` | 面板根对象 |
| `_canvasGroup` | `CanvasGroup` | 动画控制 |
| `_categoryTabs` | `TabButton[]` | 8 个分类标签（全部→法宝） |
| `_itemGridContent` | `Transform` | ScrollView/Viewport/Content（GridLayoutGroup） |
| `_itemPrefab` | `InventoryItemUI` | 物品格子预制体 |
| `_sortDropdown` | `Dropdown` | 排序下拉（获得时间/名称/品质/数量/类型） |
| `_sortDescToggle` | `Toggle` | 降序开关 |
| `_equipmentPanel` | `EquipmentPanel` | 拖入左侧装备面板 |
| `_detailPanel` | `ItemDetailPanel` | 拖入右侧详情面板 |
| `_quickSlotPanel` | `QuickSlotConfigPanel` | 拖入底部快捷栏 |
| `_openAnimDuration` | `float` | 打开动画时长，默认 0.2s |
| `_openCurve` | `AnimationCurve` | 缓动曲线 |

**TabButton 结构体**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Button` | `Button` | 点击切换 |
| `Highlight` | `Image` | 选中高亮下划线 |
| `Label` | `Text` | 标签文字 |

**按钮绑定**（在 `Start()` 中自动完成，无需手动设置）：

| 操作 | 触发 | 响应 |
|------|------|------|
| Tab/点击分类 | `SelectCategory(...)` | `InventoryService.ActiveCategory = ...` + `RefreshItemGrid()` |
| 排序下拉变化 | `OnSortChanged(int)` | `InventoryService.ActiveSortMode = ...` + `RefreshItemGrid()` |
| 降序Toggle | `OnValueChanged(bool)` | `InventoryService.SortDescending = ...` + `RefreshItemGrid()` |
| 点击物品格子 | `OnItemSelected(ItemSlot)` | `_detailPanel.Show(slot)` |
| 关闭 | Tab/Esc | `Hide()` → `MarkAllAsViewed()` |

**分类 Tab 与 ItemCategory 的索引对应**：

| 索引 | ItemCategory | 标签文字 |
|------|-------------|----------|
| 0 | All | 全部 |
| 1 | Weapon | 武器 |
| 2 | Armor | 防具 |
| 3 | Consumable | 消耗品 |
| 4 | Material | 材料 |
| 5 | Quest | 任务物品 |
| 6 | Spirit | 精魄 |
| 7 | Treasure | 法宝 |

---

### 5.2 InventoryItemUI（物品格子预制体）

**尺寸**：80×100（含图标+文字）。挂载在 Content/GridLayoutGroup 下。

| 字段 | 类型 | 说明 | 美术需求 |
|------|------|------|----------|
| `_icon` | `Image` | 物品图标 | 64×64 居中，PreserveAspect |
| `_rarityBorder` | `Image` | 品质颜色边框 | 72×72 圆角矩形，颜色由代码设定 |
| `_nameText` | `Text` | 物品名称 | 16号白色，单行，溢出省略 |
| `_countText` | `Text` | 数量 "x99" | 14号右下角 |
| `_newBadge` | `GameObject` | 新物品标记 | 金色星形/圆点，右上角 16×16 |
| `_button` | `Button` | 点击选中 | 覆盖整个格子 |

**品质边框颜色（代码自动设置）**：

| ItemRarity | 颜色 | 色值 |
|-----------|------|------|
| Common (凡品) | 灰色 | `#B3B3B3` |
| Uncommon (良品) | 绿色 | `#4DE64D` |
| Rare (上品) | 蓝色 | `#4D80FF` |
| Epic (特品) | 紫色 | `#CC4DFF` |
| Legendary (仙品) | 金色 | `#FFB333` |

**交互**：点击 → `OnItemSelected(slot)` → `ItemDetailPanel.Show(slot)`。右键 → 快捷装备（可选扩展）。

---

### 5.3 EquipmentPanel（装备栏）

**尺寸**：200×500，背包左侧。

| 字段 | 类型 | 说明 |
|------|------|------|
| `_slots` | `EquipmentSlotUI[9]` | 9 个装备槽位 |

**EquipmentSlotUI 结构体**：

| 字段 | 类型 | 说明 | 美术需求 |
|------|------|------|----------|
| `SlotType` | `EquipmentSlotType` | 槽位类型枚举 | - |
| `Icon` | `Image` | 装备图标 | 48×48 |
| `EmptyIcon` | `Image` | 空槽默认图标 | 48×48 灰色半透明轮廓 |
| `SlotName` | `Text` | 槽位名称 | 14号灰色 "武器"/"头部"... |
| `SlotButton` | `Button` | 点击卸下 | - |
| `HighlightBorder` | `Image` | 选中高亮 | 56×56 金色边框 |

**槽位排列**（垂直，从上到下）：

```
装备栏 (200×500)
├── 武器  Weapon     ┌──────┐ [图标或空]
├── 头部  Head       │      │
├── 衣甲  Body       │      │
├── 臂甲  Arms       │      │
├── 腿甲  Legs       │      │
├── 饰品1 Accessory1 │      │
├── 饰品2 Accessory2 │      │
├── 精魄  Spirit     │      │
└── 法宝  Treasure   └──────┘
```

**交互**：点击槽位 → 卸下装备 → `EquipmentManager.Unequip()` → 物品退回背包 → 事件通知 UI 刷新。悬停时，右侧 `ItemDetailPanel` 显示该装备详情。

---

### 5.4 ItemDetailPanel（物品详情）

**尺寸**：280×500，背包右侧。

| 字段 | 类型 | 说明 | 美术需求 |
|------|------|------|----------|
| `_icon` | `Image` | 物品大图标 | 96×96 居中 |
| `_nameText` | `Text` | 物品名称 | 28号白色 |
| `_rarityText` | `Text` | 品质文字 | 20号，颜色跟随品质 |
| `_categoryText` | `Text` | 分类文字 | 16号灰色 |
| `_descriptionText` | `Text` | 物品描述 | 18号白色，多行 |
| `_flavorText` | `Text` | 背景故事（装备） | 14号灰色斜体 |
| `_propertyGrid` | `Transform` | 属性行容器 | VerticalLayoutGroup |
| `_propertyRowPrefab` | `PropertyRowUI` | 属性行预制体 | 一行 属性名 + 数值 + 差异 |
| `_positiveColor` | `Color` | 正向差异颜色 | 绿色 `#4DE64D` |
| `_negativeColor` | `Color` | 负向差异颜色 | 红色 `#FF4444` |
| `_equipUseButton` | `Button` | 装备/使用按钮 | 蓝色按钮 200×40 |
| `_equipUseLabel` | `Text` | 按钮文字 "装备"/"使用"/"已装备" | 22号白色 |
| `_unequipButton` | `Button` | 卸下按钮 | 灰色按钮，仅已装备时显示 |
| `_quickSlotButton` | `Button` | 放入快捷栏按钮 | 绿色按钮 |
| `_discardButton` | `Button` | 丢弃按钮 | 红色按钮 |
| `_favoriteButton` | `Button` | 收藏按钮 | 星形按钮 |
| `_favoriteIcon` | `Image` | 收藏状态图标 | 空心/实心星形 |

**PropertyRowUI 预制体结构**：

```
PropertyRow (HorizontalLayoutGroup, 240×24)
├── PropertyName (Text "攻击力", 18号白色, 左对齐)
├── PropertyValue (Text "50", 18号白色, 右对齐)
└── DiffValue (Text "(+5)", 16号, 初始隐藏, 绿增/红减)
```

**按钮逻辑**：

| 物品类型 | 是否已装备 | 装备/使用按钮 | 卸下按钮 | 快捷栏按钮 |
|----------|-----------|-------------|----------|-----------|
| 武器/防具 | 否 | "装备" | 隐藏 | 隐藏 |
| 武器/防具 | 是 | "已装备"(禁用) | 显示 | 隐藏 |
| 消耗品 | - | "使用" | 隐藏 | 显示 |
| 材料 | - | 隐藏 | 隐藏 | 隐藏 |
| 任务物品 | - | 隐藏 | 隐藏 | 隐藏 |
| 精魄/法宝 | 否 | "装备" | 隐藏 | 隐藏 |
| 精魄/法宝 | 是 | "已装备"(禁用) | 显示 | 隐藏 |

---

### 5.5 QuickSlotConfigPanel（快捷栏配置）

**尺寸**：600×60，背包底部。

| 字段 | 类型 | 说明 |
|------|------|------|
| `_slots[0-3]` | `QuickSlotConfigUI` 数组 | 4 个配置槽位 |

**QuickSlotConfigUI 结构体**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Icon` | `Image` | 物品图标 40×40 |
| `NameText` | `Text` | 物品名称 16号 |
| `CountText` | `Text` | 数量 "x5" |
| `KeyHint` | `Text` | 按键提示 "↑" |

**操作**：在物品详情页点击"放入快捷栏"→找到第一个空槽位→`QuickSlotManager.AssignSlot(i, item)`→事件通知 HUD 刷新。

---

## 六、跨平台操作映射

### 全局

| 功能 | 键盘鼠标 | 手柄 (Xbox) | 备注 |
|------|---------|------------|------|
| 打开/关闭背包 | `Tab` | `View` (Select) | |
| 关闭面板/返回 | `Esc` | `B` | |
| 快捷栏0(上) | `↑` | `D-Pad Up` | |
| 快捷栏1(下) | `↓` | `D-Pad Down` | |
| 快捷栏2(左) | `←` | `D-Pad Left` | |
| 快捷栏3(右) | `→` | `D-Pad Right` | |

### 背包内

| 功能 | 键盘鼠标 | 手柄 | 备注 |
|------|---------|------|------|
| 分类切换 | 点击 Tab | `LB` / `RB` | 循环 |
| 物品选择 | 鼠标悬停 | 左摇杆/D-Pad | GridLayout 按行列移动 |
| 选中物品详情 | 点击 | `A` | |
| 装备/使用 | 左键点击 | `A` | |
| 卸下装备 | 点击装备槽 | `X` | |
| 放入快捷栏 | 点击按钮 | `Y` | |
| 排序切换 | 点击 Dropdown | `View` | |
| 排序方向 | 点击 Toggle | `Menu` | |
| 丢弃物品 | 点击按钮 | 长按 `X` | 二次确认 |

### 手柄焦点导航规则

```
背包打开时:
  默认焦点 → 第一个物品格子(0,0)
  
  D-Pad Up/Down    → 上下移动焦点行
  D-Pad Left/Right → 左右移动焦点列
  左摇杆向左(在网格第一列) → 焦点跳到装备栏
  左摇杆向右(在网格最后一列) → 焦点跳到详情面板
  在装备栏中 LB/RB → 上下遍历装备槽位
  
  按 A → 
    if 焦点在物品格子 → 选中显示详情
    if 焦点在装备槽位 → 卸下
    if 焦点在详情面板按钮 → 执行按钮操作
  
  按 B → 关闭背包
```

---

## 七、美术资源需求清单

### 图标资源

| 资源 | 规格 | 数量 | 说明 |
|------|------|------|------|
| 物品图标 | 128×128 PNG | 按物品总数 | 武器/防具/消耗品/材料/任务物品 |
| 品质边框 | 80×80 PNG | 5 张 | 白/绿/蓝/紫/金 圆角矩形边框 |
| 装备空槽图标 | 48×48 PNG | 9 张 | 武器/头/衣/臂/腿/饰品/精魄/法宝 剪影 |
| 按键图标 | 32×32 PNG | 10+ | E/Tab/↑↓←→/A/B/X/Y/LB/RB |
| 状态效果图标 | 32×32 PNG | 按效果数量 | 灼烧/减速/中毒/增益等 |
| 精魄/法宝技能图标 | 64×64 PNG | 按技能数量 | |
| 新物品标记 | 16×16 PNG | 1 | 金色星形/圆点 |

### UI 切图

| 资源 | 规格 | 说明 |
|------|------|------|
| 背包面板背景 | 850×700 九宫格 | 深色半透明底 + 边框装饰 |
| 详情面板背景 | 280×500 九宫格 | 略浅色调 |
| 血条底图 | 220×18 灰色条 | HP/MP/SP 共用 |
| 血条填充 | 220×18 红色渐变 | |
| 法力填充 | 220×14 蓝色渐变 | |
| 耐力填充 | 220×14 黄色渐变 | |
| 棍势圆点(空) | 24×24 灰色圆形 | |
| 棍势圆点(满) | 24×24 金色圆形 | |
| Boss 血条底图 | 560×24 深色 | |
| Boss 血条填充 | 560×24 红色渐变 | |
| 技能冷却遮罩 | 64×64 灰色半透明 | fillMethod=Top |
| 技能就绪光晕 | 72×72 金色辉光 | |
| 快捷栏槽位底图 | 64×64 圆形 | 深色半透明 |
| 快捷栏葫芦槽位 | 64×64 圆形 | 金色边框(槽位0) |
| 按钮(装备/使用) | 200×40 蓝色圆角 | |
| 按钮(卸下) | 200×40 灰色圆角 | |
| 按钮(丢弃) | 200×40 红色圆角 | |
| 按钮(收藏) | 40×40 星形 | 空心/实心两张 |
| Tab 选中下划线 | 100×3 金色 | |
| 弹窗背景 | 200×60 深色半透明 | 拾取提示用 |
| 交互提示背景 | 200×60 深色半透明 | |

### 字体

| 用途 | 建议字体 | 大小 |
|------|---------|------|
| 物品名称 | 思源黑体 Medium | 16-28 |
| 数值/数量 | 思源黑体 Bold | 14-32 |
| 描述文字 | 思源宋体 Regular | 14-18 |
| 冷却倒计时 | 数字专用字体 | 32 Bold |
| 按键提示 | 思源黑体 Regular | 12-16 |

### 预制体

| 预制体 | 挂载脚本 | 说明 |
|--------|---------|------|
| ItemCell | `InventoryItemUI` | 物品格子(80×100) |
| PropertyRow | `PropertyRowUI` | 属性行(240×24) |
| FloatingText_Damage | `FloatingText` | 伤害数字 |
| FloatingText_Heal | `FloatingText` | 治疗数字 |
| FloatingText_Crit | `FloatingText` | 暴击数字 |

---

## 八、代码调用指南

### 从旧系统迁移

```csharp
// 旧代码
InventoryManager.Instance.AddItem(itemSO);

// 新代码 —— 同一行，效果相同
InventoryService.Instance.AddItem(itemSO);
// ↑ ItemSO 通过 ItemAdapter 自动适配 IInventoryItem，无需修改 ItemSO
```

### 拾取物品

```csharp
// 玩家捡起物品时
void OnPickUp(ItemSO item) {
    int added = InventoryService.Instance.AddItem(item);
    if (added > 0) {
        // 音效、特效等由订阅 InventoryEvents.OnItemAdded 的系统处理
    } else {
        // 背包已满提示
        MessageUI.Instance.Show("背包已满");
    }
}
```

### 使用消耗品

```csharp
// 快捷栏使用
QuickSlotManager.Instance.UseSlot(0);  // 使用↑槽位道具

// 直接通过ID使用
InventoryService.Instance.UseItem("3");  // id=3 的消耗品
```

### 装备/卸下

```csharp
// 装备武器
var weapon = ...; // IEquipment
var oldWeapon = EquipmentManager.Instance.Equip(weapon);
if (oldWeapon != null)
    InventoryService.Instance.AddItem(oldWeapon as ItemSO); // 旧装备退回背包

// 卸下头盔
var helmet = EquipmentManager.Instance.Unequip(EquipmentSlotType.Head);
if (helmet != null)
    InventoryService.Instance.AddItem(helmet);
```

### HUD 更新（由 PlayerProperty 驱动）

```csharp
// 在 PlayerProperty 的 Update 或事件中：
var hud = GetComponent<PlayerStatusHUD>();
hud.SetHP(playerProperty.hpValue, playerProperty.maxHp);
hud.SetMP(playerProperty.energyValue, playerProperty.maxEnergy);
hud.SetSP(playerProperty.staminaValue, playerProperty.maxStamina);
hud.SetChargePoints(combatResource);  // 0-4
```

### 浮动数字

```csharp
// 敌人受击
FloatingTextManager.Instance.ShowDamage(hitPoint, damage, isCrit: Random.value < critChance);

// 玩家治疗
FloatingTextManager.Instance.ShowHeal(playerTransform.position, healAmount);
```

### 交互提示

```csharp
// 玩家靠近可交互物
prompt.Show(keyIconE, "拾取");
// 玩家离开
prompt.Hide();
```

### 音效集成

```csharp
// 在 AudioManager 中订阅事件，不改 Inventory 代码
void Start() {
    InventoryEvents.OnItemAdded += (item, count) => PlaySFX("pickup");
    InventoryEvents.OnEquipmentChanged += (slot, item) => PlaySFX("equip");
    InventoryEvents.OnNewItemObtained += (item) => PlaySFX("new_item");
}
```

---

## 九、与旧系统共存策略

| 旧系统 | 新系统 | 关系 |
|--------|--------|------|
| `InventoryManager` (单例) | `InventoryService` (单例) | **并存**，逐步迁移调用方 |
| `WeaponManager` (4槽位) | `EquipmentManager` (9槽位) + `QuickSlotManager` (4槽) | **并存**，WeaponManager 继续管理旧武器槽位 |
| `PlayerPropertyUI` (旧HUD) | `PlayerStatusHUD` (新HUD) | **并存**，逐步替换，两套 HUD 可同时显示 |
| `InventoryUI` (旧背包) | `InventoryPanel` (新背包) | **新 Tab，旧 B**，暂不冲突 |
| `ItemSO` (ScriptableObject) | `IInventoryItem` (接口) | **适配器**，ItemSO 不修改，通过 ItemAdapter 适配 |
| `ItemType` 枚举 (Weapon/Consumable) | `ItemCategory` 枚举 (8种) | `ItemAdapter.MapCategory()` 做映射 |

**迁移步骤建议**：
1. 先在场景中挂载新系统（不影响旧系统运行）
2. 逐步把调用方从 `InventoryManager` 改为 `InventoryService`
3. 美术资源到位后，隐藏旧 HUD，启新 HUD
4. 旧系统完全弃用后，删除旧脚本
