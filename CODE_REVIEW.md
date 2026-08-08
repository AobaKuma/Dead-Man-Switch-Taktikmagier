# Taktikmagier 模組 — 檢查與修復紀錄

檢查基準：RimWorld 1.6 / VEF / VanillaPsycastsExpanded 1.6 / Dead Man Switch Core
方法：反編譯比對 `VanillaPsycastsExpanded.dll`、`VEF.dll`、`Assembly-CSharp.dll`，交叉核對本模組 XML 與 C# 來源。

> 本輪已修復全部探明問題。下方保留原始診斷以便追溯，並在每項標註修法。

---

## 一、核心架構變更

### 構裝體熱量：從「自製一半」改為「雙軌，各走正確的路」

反編譯確認 VPE 內建一套完整機制：

```
CompBreakLink.PostSpawnSetup()  → if (parent is IMinHeatGiver g) pawn.Psycasts().AddMinHeatGiver(g)
Hediff_PsycastAbilities.Tick()  → minHeatGivers.RemoveAll(g => g == null || !g.IsActive) → Recache
Pawn_Construct : IMinHeatGiver  → IsActive => Spawned;  MinHeat => 20
```

原本的 `Hediff_Focus` 重造了這套輪子，但只造了「登記」沒造「回收」。

**修法沒有全面倒向 VPE**，因為砲塔走不通：`DMST_Turret_Sentry` 繼承 `DMS_BaseCannonBuilding`，而 CE 會用 `PatchOperationReplace` 把該抽象父級的 `thingClass` 整個換成 `CombatExtended.Building_TurretGunCE`。若替砲塔指定自訂 `thingClass` 來實作 `IMinHeatGiver`，CE 的 patch 就打不到它，CE 環境下砲塔會壞掉。因此：

| 構裝體 | 熱量路徑 | 理由 |
|---|---|---|
| **Gargoyle**（pawn） | VPE 原生 `IMinHeatGiver` | `Pawn_Construct` 已實作，`CompBreakLink` 自動註冊、自動剔除、自動存讀檔 |
| **Sentry turret**（building） | 修好的 `Hediff_Focus` | 建築的 `thingClass` 被 CE 佔用，無法實作介面 |

兩條路徑的熱量值都從構裝體 ThingDef 上的 `Militarmagier.ConstructHeatExtension` 讀取，成為單一真實來源。

---

## 二、問題與修法

### 🔴 嚴重

**A1. 構裝體死亡後熱量永不歸還** — ✅ 已修
`Hediff_Focus` 只有 `AddHeatGiver()`，`heatGiver` 字典從不清理。def 描述明寫會歸還，實際不會；每召喚一次就永久 +50 最低熵。

> 改為 `List<Thing> heatGivers`，每 60 tick 於 `TickInterval()` 剔除 `null / Destroyed / !Spawned` 的項目並 recache。

**A2. Gargoyle 熱量雙重計算** — ✅ 已修
`Pawn_ConstructWeaponUsable : Pawn_Construct` 已透過 `CompBreakLink` 被 VPE 記 **+20**，`AbilityExtension_ConstructPawn` 又在 `Hediff_Focus` 記 **+50**，實際 70。

> `AbilityExtension_ConstructPawn` 不再碰 `Hediff_Focus`；改由 `Pawn_ConstructWeaponUsable` 重新實作 `IMinHeatGiver` 並覆寫 `MinHeat`。
> `Pawn_Construct.MinHeat` 是 **non-virtual** 的 `=> 20`，無法一般覆寫；但在衍生類重新列出 `IMinHeatGiver` 會讓 C# 重建介面對映指向最衍生成員，而 VPE 一律透過介面讀取（`minHeatGivers.Sum(g => g.MinHeat)`），因此生效。

**A3. `Notify_PawnDied` 對已摧毀物件呼叫 `Kill()`** — ✅ 已修
因 A1 字典裡累積歷史上所有構裝體，施法者死亡時對已 destroyed 的 Thing 呼叫 `Kill()` → `Log.Error` 洗版。

> 整個 override 移除。`CompBreakLink.CompTick()` 本來就會在 caster 死亡時自動清理構裝體，這段從頭到尾是多餘的。

**A4. 存檔往返產生 null key** — ✅ 已修
`LookMode.Reference` 無法解析已 destroyed 的 Thing → 讀檔後出現 null，`ShowCost()` 與 `Notify_PawnDied()` 雙雙 NRE。

> `ExposeData` 的 `PostLoadInit` 階段補 null-init 與 `RemoveAll(t => t == null || t.Destroyed)`，`CostBreakdown()` 亦跳過 null。

**A5. About.xml 宣稱支援 1.5，但 1.5 完全載入不了** — ✅ 已修
`LoadFolders.xml` 只有 `<v1.6>` 節點。RimWorld 在 LoadFolders 存在但缺當前版本節點時，該版本**一個檔案都不載入**——1.5 玩家拿到空模組。

> 移除 `<li>1.5</li>`，並在 About.xml 留註說明要恢復 1.5 需要同時補 `<v1.5>` 節點與 1.5/Assemblies。

---

### 🟡 中等

**B1. `Ability_Repel` 位移計算會把目標拉近** — ✅ 已修
掃描從 **origin** 起算，遇障礙即 `break`。若施法者與目標間有掩體，`end` 會停在比目標原位更靠近施法者的格子 → 擊退變拉近。

> 改為從 **目標所在格** 往外掃 `GenSight.PointsOnLineOfSight(targetPos, destination)`。

**B2. ~~Repel 雙重暈眩~~** — ⚠️ **原診斷有誤，非問題**
複查後撤回。`AbilityExtension_Explosion.explosionDamageAmount` 預設 `-1`，`GenExplosion` 遂改用 `damageDef.defaultDamage`，而 `DMST_RepelShockWave` 明寫 `<defaultDamage>0</defaultDamage>`；`StunHandler` 對 amount 0 直接跳過（`if (num > 0)`）。**爆炸純粹是視覺與音效**，唯一的暈眩來自 `Cast()` 裡的 `TakeDamage`。

> 未改行為，僅在 XML 與 C# 補上註解說明這層關係，避免日後有人「修掉」它。

**B3. `Additional_PawnGroup.xml` xpath 命中過多節點** — ✅ 已修
`DMS_Army` 底下有 4 組 `kindDef=Combat`，含純機械組（`DMS_Mech_Dogge` / `DMS_Mech_Falcon`）。`PatchOperationAdd` 會對所有命中節點執行 → Taktikmagier 混進只該出機械的襲擊組。

> xpath 加上 `[options/DMS_Soldier]` 述詞，鎖定人類步兵組。

**B4. `AbilityExtension_Calm` 未檢查 `needs.mood`** — ✅ 已修
`rest` 檢查了但 `mood` 沒有。

> 改為 `pawn?.needs == null` 早退 + `pawn.needs.mood?.thoughts?.memories?.TryGainMemory(thought)`。

**B5. `cost.SplitOff(1)` 回傳值被丟棄** — ✅ 已修
`stackCount > 1` 時產生一個新的未 spawn Thing 並丟棄；`== 1` 時 `DeSpawnOrDeselect()` 後回傳 `this`，同樣丟棄。兩種情況都留下未註冊的孤兒 Thing。另外 `GenSpawn.Spawn()` 排在 `SplitOff()` 之前，靠「砲塔 passability 不是 Impassable、`SpawningWipes` 不會清掉腳下物品」才沒出事。

> 抽出 `AbilityExtension_ConstructBase.TryConsumeCost()`：先記下 `Position`/`Map`，再 `cost.SplitOff(1).Destroy()`，最後才 `GenSpawn.Spawn()`。

**B6. `DMST_ConstructPawn` def 重複與衝突欄位** — ✅ 已修
`<castTime>` 寫兩次（60 與 360，後者生效）；`targetMode` 與 `targetingParameters` 並存，而 `AbilityDef.PostLoad()` 在後者存在時整段 targetMode switch 會被跳過，留下對不上的 `targetModes[0]`。

> 只保留 `<castTime>360</castTime>`，刪掉 `<targetMode>Pawn</targetMode>`，並加註說明兩者不該同時出現。

**B7. `DMST_Demolish` 無代價蒸發建築且不掉材料** — ✅ 已修（依指示只做掉落）
`Destroy()` 預設 `DestroyMode.Vanish`，不留任何資源或殘骸。

> 改為 `building.Destroy(DestroyMode.KillFinalize)`，拆除會留下殘料與殘骸，符合「解體」的設定。
> 另外 `undestroyFactor` 的結果加上 `Mathf.Max(1, …)`，避免整除成 0 HP 的殘破建築。
> **未加**陣營限制與岩石排除（依你的選擇保留現狀）。

---

### 🔵 輕微 / 整理

| 項目 | 狀態 | 修法 |
|---|---|---|
| **C1** 發布含 `.vs/`、`bin/Debug`（100+ 顆遊戲 DLL）、`obj/` | ✅ | 新增 `.gitignore`。**注意：既有檔案未刪除**，因為 `bin/Debug` 目前是 csproj 的參考來源之一；請自行確認打包腳本有排除 `.source/` |
| **C2** 無 `Languages/`、多處 placeholder、中英混雜 | ✅ | def 內文全部改英文；中文移入 `Languages/ChineseTraditional/DefInjected/`；補上 `Keyed` 字串；`repel.` / `swap.` / `quick draw.` / `focus.` 等佔位全部改寫；backstory 由 1 個補到 3 個並移除 `<!-- TODO -->` |
| **C3** defName 前綴 `DMS_` / `DMST_` 混用 | ✅ | 全面統一為 `DMST_`（含 `DMST_RepelShockWave`、`DMST_Fleck_RepelShockwave`、`DMST_PsycastFocus`），CE patch 同步更新。**依你的指示未做存檔相容處理** |
| **C4** `* 2` 魔法數字 | ✅ | 改為具名常數 `StunTicksPerDamagePoint = 30f` 與 `GetDurationForPawn() / 30f`，並註明 `StunHandler` 的 `amount * 30` 換算 |
| **C5** `DamageDefOf.Stun` 觸發機械 stun 適應 | ⚠️ 未改 | 需自訂不帶 `stunAdaptationTicks` 的 DamageDef，屬設計取捨而非 bug，留給你決定 |
| **C6** `tmpCells` 共用實例欄位 | ✅ | 更名 `cachedCells`、改 private，並註明「回傳的是共用緩衝、只在下次呼叫前有效」——刻意保留重用以避免 `DrawHighlight` 每幀配置 |
| **C7** `(Building)target.Thing` 硬轉型 | ✅ | 改為 `is Building building` 模式比對 |
| **C8** `TryGetComp<CompBreakLink>()` 未檢查 null | ✅ | 集中到 `LinkToCaster()`，null 時 `Log.ErrorOnce` 而非 NRE |
| **C9** 兩個技能同為 level 3 / order 1 | ✅ | 改為 order 1 / 2 / 3（`ConstructTurret` / `ConstructPawn` / `QuickDraw`） |
| **C10** `Hediff_Focus` 未設 `becomeVisible` | ✅ | 確認是刻意可見（有 `CostBreakdown()`），保留並補註 |

---

## 三、實作過程中額外發現並修掉的

- **`ShouldRemove` 的嚴重陷阱**：`Hediff.ShouldRemove` 的實作是 `Severity <= 0f`。原先寫成 `heatGivers.Count == 0 || base.ShouldRemove`，會讓 hediff 的生命週期意外綁到一個本模組從不使用的 severity 值上。已改為只依 `heatGivers.Count == 0`，並加註說明為何刻意不呼叫 base。
- **`TickInterval` vs `Tick`**：1.6 的 `Hediff.Tick()` 是空的殘留方法，`Pawn_HealthTracker.HealthTickInterval()` 呼叫的是 `TickInterval(delta)` 再呼叫 `PostTickInterval(delta)`。覆寫對象選用前者才會被呼叫到。
- **`MilitarmagierDefOf`** 補上標準的 `EnsureInitializedInCtor` 靜態建構式；`VPE_PsychicEntropyMinimum` 刻意不加 `[MayRequire]`（VPE 是硬相依，寧可拿到 DefOf 錯誤也不要靜默 null）。
- `Ability_Swap` / `Ability_Flash` / `AbilityExtension_Heal` 補上 `Map` 與空目標的防護。
- 新增 `AbilityExtension_ConstructBase` 收攏兩個召喚技能重複的目標驗證與扣料邏輯。

---

## 四、驗證狀態

| 驗證 | 結果 |
|---|---|
| 全部 XML well-formed | ✅ 通過 |
| 舊 defName 殘留掃描 | ✅ 無殘留 |
| XML 引用的 `Militarmagier.*` 類別皆存在 | ✅ 11/11 對上 |
| 模組內部 `DMST_*` def 引用皆可解析 | ✅ 無懸空引用 |
| C# 括號配對 / namespace | ✅ 13 檔全過 |
| 反編譯核對 API 簽章 | ✅ `TickInterval` / `ShouldRemove` / `Messages.Message` / `DamageInfo` ctor / `GenSight.LineOfSight` / `SplitOff` / `SpawningWipes` 皆已確認 |
| **實際編譯** | ❌ **未執行** — 沙箱無 C# 編譯器 |
| **遊戲內執行** | ❌ **未執行** |

> ⚠️ 上線前請務必在 Visual Studio 實際 build 一次（`.csproj` 已加入兩個新檔），並在遊戲內測試：召喚砲塔與 gargoyle → 確認最低熵各 +50、摧毀後歸還、hediff 自動消失 → 存讀檔一輪 → 施法者死亡不噴錯。

---

*檢查範圍：`1.6/Defs/**`、`1.6/CE/Patches/**`、`Patches/**`、`About/`、`LoadFolders.xml`、`.source/Militarmagier/*.cs`。未涵蓋美術資產與 CE 數值平衡。*

---
---

# 第二輪：隱身機制與 AI 技能使用

## 五、隱身機制

### 原本的狀態

| 項目 | 現況 | 問題 |
|---|---|---|
| 觸發條件 | 只需穿 `DMST_Apparel_MagierCloak` | 頭盔寫「complete invisibility requires wearing a magier cloak」、斗篷寫「requires a magier helmet to be effective」——**兩個描述互相指涉的套裝需求從未被實作** |
| 成本 | 無 | def 裡的 `Ability_EntropyGain` 30 / `Ability_PsyfocusCost` 0.06 被註解掉，等於免費 45 秒隱身 |
| 開火行為 | 不現形 | 香草 `HediffComp_Invisibility` 行為：只有受傷、燃燒、倒地、被暈眩（非玩家陣營）、干擾閃光、消防泡沫會強制現形。**沒有任何程式碼在攻擊時呼叫 `BecomeVisible`** |

### 已做的調整

**✅ 強制頭盔 + 斗篷套裝**（依你的選擇）

新增 `Militarmagier.CompProperties_AbilityRequireApparel` / `CompAbilityEffect_RequireApparel`，掛在 `DMST_Camouflage` 上：

```xml
<li Class="Militarmagier.CompProperties_AbilityRequireApparel">
    <requiredApparel>
        <li>DMST_Apparel_MagierHelmet</li>
        <li>DMST_Apparel_MagierCloak</li>
    </requiredApparel>
</li>
```

關鍵細節：**覆寫的是 `AbilityComp.CanCast` 而不只是 `GizmoDisabled`**。查 `RimWorld.Ability.CanCast` 會逐一詢問 comps 的 `CanCast`，而 AI 施法路徑走的是 `CanCast`——只擋 gizmo 的話玩家被限制、敵方 AI 卻照用不誤。`GizmoDisabled` 另外覆寫是為了讓玩家看到「需要穿戴軍術士頭盔」而不是一個無聲失效的按鈕。

**✅ 開火不現形，與原版 DMS 一致**（依你的選擇）——未改動任何行為，只在 def 上加註說明這是刻意跟隨 `DMS_Camouflage` 的設計，避免日後被當成漏洞「修掉」。

**⚠️ 未做**：施法成本與隱身期間的持續熱量消耗。斗篷描述仍寫著「consumes psychic entropy」，目前仍是空話。若之後要補，把註解掉的 `statBases` 放回去即可。

### 順帶記錄的一個發現

`DMST_Camouflage` 繼承 `DMS_AddHediffSelfBase`，而該 base 帶 `<groupDef>DMS_AutomatroidAbility</groupDef>`，那個 `AbilityGroupDef` 的 `cooldownTicks` 是 **7200**。所以隱身的實際冷卻是 2 小時遊戲時間的群組冷卻，而不是 def 上看得到的任何數字。原版 `DMS_Camouflage` 也一樣，因此**未改動**——但如果你覺得冷卻不對勁，原因在這裡。

---

## 六、AI 技能使用

### 先修正我自己的一個誤判

我一度從反編譯的欄位清單讀到 `public float chance;` 而推論「預設 0，所以 AI 從不施法」。**這是錯的。** 欄位初始值在建構式 IL 裡，實際是：

```
IL_0153: ldc.r4 1
IL_0158: stfld VEF.Abilities.AbilityDef.chance      // chance = 1
IL_0001: ldc.i4.1
IL_0002: stfld VEF.Abilities.AbilityDef.targetCount // targetCount = 1
```

`chance` 預設 **1**、`targetCount` 預設 **1**。這也解釋了為什麼 VPE 自己在 56 個 def 上明寫 `<chance>0</chance>`——那是在**關掉**預設就開著的 AI 自動施放。

### AI 的實際運作方式

唯一的路徑是 VEF 對 `Pawn.TryGetAttackVerb` 的 postfix：

```csharp
list = comp.LearnedAbilities
    .Where(ab => ab.AutoCast && ab.IsEnabledForPawn(out _) && (target == null || ab.CanHitTarget(target)))
    .Select(ab => ab.verb);

// 與武器 verb（權重 1）一起抽籤，權重 = ability.Chance
(from ve in list where ve.ability.AICanUseOn(target) select (ve, ve.ability.Chance))
    .AddItem((__result, 1f))
    .TryRandomElementByWeight(...)
```

兩個推論：

1. **抽中的技能會「取代」那一次射擊**。任何不是真正攻擊手段的技能都必須 `chance 0`。
2. **`CanHitTarget(敵方 pawn)` 是硬門檻**。Self / Location / 物品目標的技能永遠過不了，AI 根本碰不到。

逐一實測結果：

| 技能 | 目標模式 | AI 是否碰得到 | 原本行為 |
|---|---|---|---|
| DMST_Flash | Location | ❌ | 這個模組的招牌技能，敵人從來不會用 |
| DMST_Repel | Self | ❌ | 同上 |
| DMST_EmpBurst | Self | ❌ | 同上 |
| DMST_QuickDraw | Self | ❌ | 同上 |
| DMST_ForceWake | Self | ❌ | — |
| DMST_Demolish | 僅建築 | ❌ | — |
| 兩個召喚 | 僅物品 | ❌ | — |
| **DMST_Swap** | Pawn | ✅ | 敵方軍術士會**隨機與玩家小人換位** |
| **DMST_FieldAid** | Pawn | ✅ | `isPositive` 未設 → `AICanUseOn` 一律回 true → 敵方軍術士會**幫玩家的小人治療** |

### 已做的調整

**✅ XML 止血**——全部 10 個 VEF 技能都補上明確的 `chance` 與 `isPositive`，並在檔案頂端寫了一段說明，解釋這兩個欄位的預設值陷阱與「技能會取代射擊」這件事。`DMST_FieldAid` → `isPositive true`、`DMST_Swap` → `chance 0`。

**✅ 自訂 JobGiver**——新增 `Militarmagier.JobGiver_TaktikmagierCombat`，透過 `Patches/AI_ThinkTree.xml` 插入 `HumanlikeConstant` 思考樹，讓敵方軍術士真的會用那四個 AI 原本碰不到的技能：

| 優先序 | 技能 | 觸發條件 |
|---|---|---|
| 1 | Repel | 4.9 格內有 ≥2 個敵對單位（被貼身圍毆時脫離） |
| 2 | EMP burst | 技能半徑內有 ≥2 台敵對機械（對血肉幾乎無效，不浪費） |
| 3 | Flash | 目標在射程內且過得了 `CanHitTarget` |
| 4 | Quick draw | 敵人在 9.9 格內、持遠程武器、身上還沒有該 buff |

設計上的幾個取捨：

- **插在 `HumanlikeConstant` 而非主思考樹**：常駐樹不管當前 job 都會評估，這才能讓 AI 中斷射擊去放閃光。代價是每個人形單位都會走到這個節點，所以 `TryGiveJob` 開頭就用「非玩家陣營 → 已生成且未倒地 → 每 30 tick 一次 → 有敵方目標」四層便宜的檢查擋掉絕大多數呼叫。
- **用 Append 而非 Prepend**：排在香草常駐節點之後，逃離火焰、丟掉燃燒中的武器這類行為仍然優先。著火的軍術士應該跑，不是放閃光。
- **玩家小人被刻意排除**：他們有 gizmo 和 VEF 自己的 autocast 開關，自動施法只會是驚嚇。
- **`IsEnabledForPawn` 一併涵蓋了心靈集中/熵的檢查**（它會問過 `AbilityExtension_Psycast`），所以耗盡的軍術士不會硬施法。

---

## 七、第二輪的驗證狀態

| 驗證 | 結果 |
|---|---|
| 全部 XML well-formed | ✅ |
| XML 引用的 `Militarmagier.*` 類別皆存在 | ✅ |
| DefOf 欄位名皆有對應 defName | ✅（`DMST_QuickDraw` 同時是 HediffDef 與 AbilityDef，已拆成兩個 DefOf 類別） |
| C# 括號配對 / csproj 檔案清單一致 | ✅ 15/15 |
| 反編譯核對 API | ✅ `AbilityComp.CanCast`/`GizmoDisabled`、`AbilityCompProperties`、`Ability.CanCast` 聚合邏輯、`StartAbilityJob` 的 job 建構、`Thing.IsHashIntervalTick`、`VFEA_UseAbility`、`CanHitTarget`、`GetRadiusForPawn` |
| **實際編譯 / 遊戲內測試** | ❌ 皆未執行 |

### ⚠️ 這一輪風險最高的三個點

1. **`JobGiver_TaktikmagierCombat.CastJob` 動了 VEF 的內部狀態。** VEF 把「正在施放的技能」放在 `CompAbilities.currentlyCasting` 而不是 Job 上，所以我必須在回傳 Job 之前先設好它。如果思考樹最後丟棄了這個 Job，`currentlyCasting` 會殘留——下一次評估時我的 `currentlyCasting != null` 檢查會讓該 pawn 暫時不再施法，直到 job 正常結束清掉它。**這是我最想請你在遊戲裡盯的一點**：找一隻敵方軍術士，看它會不會卡住不再放技能。

2. **`HumanlikeConstant` 的 xpath。** 若未來版本改名，RimWorld 會記一筆 patch error 但不會壞檔，AI 只是退回純射擊。開遊戲後搜尋 log 有沒有 `AI_ThinkTree.xml` 相關錯誤即可確認。

3. **`chance 0` 與 JobGiver 的關係。** JobGiver 不看 `AutoCast`/`Chance`，它直接檢查 `IsEnabledForPawn` 再建 job，所以兩者不衝突——`chance 0` 只負責把技能踢出「取代射擊」的抽籤。改動時別把這兩條路徑搞混。

---
---

# 第三輪：對齊 VPE 的靈能優化與施放邏輯

## 八、VPE 的「靈能優化」實際上是什麼

`Hediff_PsycastAbilities.RecacheCurStage()` 反編譯結果，等級與優化點數提供：

| 提供 | 公式 |
|---|---|
| `PsychicEntropyMax` | `level × 5 + statPoints × 10` |
| `PsychicEntropyRecoveryRate` | `level × 0.0125 + statPoints × 0.05` |
| `PsychicSensitivity` | `statPoints × 0.05` |
| `VPE_PsyfocusCostFactor` | `statPoints × -0.01` |
| `MeditationFocusGain` | `statPoints × 0.1`（設定開啟時） |

**這一套本模組本來就吃得到**——`DMST_Taktikmagier` 的 `PawnKindAbilityExtension_Psycasts` 已經給了 `statUpgradePoints 2~6`，而 `AbilityExtension_Psycast.GetPsyfocusUsedByPawn()` 是 `psyfocusCost × GetStatValue(VPE_PsyfocusCostFactor)`，所以**心靈集中消耗早就會隨優化點數自動下降**。這部分沒有壞掉，不需要修。

### 一個我差點誤報的東西

斗篷的 `equippedStatOffsets` 有 `PsychicEntropyMax 0.75`。相對於 VPE 加的 `level×5 + statPoints×10`（動輒 +30~+90），0.75 看起來像單位寫錯。

**但查了 VPE 自己的 eltex 裝備，它給的是 `PsychicEntropyMax 0.6667`**——同一個量級。所以這是 VPE 全家的慣例（或共同的量級問題），**不是本模組的 bug**，未改動。只是要知道：這個數值實際影響接近可忽略。

---

## 九、`<psychic>` 旗標：查清楚它到底做什麼

`find_usages` 顯示 `AbilityExtension_Psycast.psychic` 全 VPE 只有 **2 處**讀取，都在同一個類別內：

1. `ValidateTarget()` — 目標 `PsychicSensitivity < epsilon` 時拒絕並顯示 "Ineffective"
2. `TargetingOnGUI()` — 瞄準時在每個目標頭上顯示其靈能感知值

**沒有其他效果**——不影響威力、不影響消耗。

推論：對**自施**技能它幾乎是空操作，因為 `IsEnabledForPawn` 早就擋掉了施法者自己感知為 0 的情況。真正有差別的只有對 pawn 施放的技能。

VPE 的慣例（29 個標 `psychic true` 的技能）是「作用於**心智**」：`VPE_FiringFocus`、`VPE_AdrenalineRush`、`VPE_Berserk`、`VPE_Mindcontrol`、`VPE_BlindingPulse`…；而 skip／位移系一律不標。

**已套用**：只有 `DMST_FieldAid`（依你的選擇）。理由寫在 def 的註解裡——戰地急救是靠靈能驅動組織增生，對靈能全聾者（機械體、感知歸零的人）本來就無從作用。其餘刻意不標：強光是真實光與噪音（描述明寫對機械傳感器有效）、排斥是物理衝擊波、EMP 是磁場、換位是 skip 效果。

---

## 十、神經熱隨優化點數縮放（已套用）

`AbilityExtension_Psycast` 有一個 `entropyGainStatFactors` 欄位：

```csharp
GetEntropyUsedByPawn(pawn) =
    entropyGainStatFactors.Aggregate(entropyGain,
        (current, f) => current * (pawn.GetStatValue(f.stat) * f.value));
```

**VPE 自己 150 個技能一個都沒用它。** 而軍術士的路線描述寫的正是「不斷精煉他們的基礎法術，以提高其效率和頻率」。

已對全部 10 個技能加上：

```xml
<entropyGainStatFactors>
    <VPE_PsyfocusCostFactor>1</VPE_PsyfocusCostFactor>
</entropyGainStatFactors>
```

`VPE_PsyfocusCostFactor` 的 `defaultBaseValue` 是 **1**（已查 VPE `Defs/StatDefs/Stats.xml` 確認），`minValue` 0。所以：

- 未投優化點數 → ×1.0，**完全不變**
- 投 6 點 → ×0.94，約省 6% 神經熱

幅度刻意保守，且用的是 VPE 原生欄位與原生 stat，沒有新增任何機制。

> XML 格式提醒：`StatModifier` 有 `LoadDataFromXmlCustom`，以**節點名稱**當 stat defName，所以寫法是 `<VPE_PsyfocusCostFactor>1</VPE_PsyfocusCostFactor>` 而不是 `<li><stat>…</stat><value>…</value></li>`。

---

## 十一、施放邏輯：把 JobGiver 補齊到與 VEF 一致

對照 `Ability.CreateCastJob()` → `StartAbilityJob()` 的實際流程，我上一輪寫的 JobGiver 漏了一段。

### ✅ 補上 `Valid()` 閘門

`CreateCastJob` 在啟動施法前會跑：

```csharp
foreach (var ext in AbilityModExtensions)
    if (!ext.Valid(targets, this, throwMessages: true)) return;
```

而 `AbilityExtension_Psycast.Valid()` 會再呼叫一次 `IsEnabledForPawn`——重新檢查心靈集中、熵是否溢出、靈能感知、以及**是否正在引導其他法術**。我原本只在挑選階段用 `Ready()` 檢查一次，之後就直接建 job。現在 `TryCastJob()` 會在提交前跑完整個 `Valid()` 迴圈，被拒絕就回傳 null 並讓決策階梯往下一個技能走。`throwMessages` 固定 false——AI 的決策不該把拒絕訊息丟到玩家臉上。

### ✅ 保留神經熱餘裕

VPE 只擋「會溢出」的施法，不管浪費。快拔在階梯最底層，若把熱量燒光就沒有排斥／強光可用了，因此加了 `EntropyFraction(pawn) < 0.5` 的門檻。

### ⚠️ 刻意不做的兩件事（已寫進註解）

- **不呼叫 `EndCurrentJob`**：思考樹正在選下一個 job，在這裡結束當前 job 會重入 job tracker。
- **不呼叫 `PreCast`**：它的契約是把「稍後啟動 job」的 callback 交給 extension，而 think node 必須當場回傳 job 或什麼都不回。本模組這四個技能都沒有覆寫 `PreCast` 的 extension（`AbilityExtension_Psycast` 沒有覆寫它），所以沒有損失。

### 🔴 一個很容易踩的坑（已寫進程式碼註解）

```csharp
// Ability.CanHitTarget:
float num = target.Cell.DistanceTo(pawn.Position);
if (target.IsValid && num < GetRangeForPawn() && ...)
```

`DMST_Repel` / `DMST_EmpBurst` / `DMST_QuickDraw` **都沒有宣告 `<range>`**，所以 `GetRangeForPawn()` 回傳 **0**。自施時距離也是 0，於是 `0 < 0` 為 **false**——`CanHitTarget(自己)` 永遠失敗。

我一度打算「順手把四個分支都改成走 `ValidateTarget`」讓程式碼更整齊，那會**靜默廢掉其中三個技能**。遊戲真正的自施路徑同樣不做距離檢查，所以現在的寫法是對的。程式碼裡留了一段警告註解，避免日後被「整理」掉。

---

## 十二、第三輪驗證狀態

| 驗證 | 結果 |
|---|---|
| 全部 XML well-formed | ✅ |
| 10 個技能的 `entropyGainStatFactors` / `psychic` / `chance` 逐一核對 | ✅ |
| `StatModifier` 的 XML 簡寫格式 | ✅ 已由 `LoadDataFromXmlCustom` 反編譯確認 |
| `VPE_PsyfocusCostFactor` 基礎值 = 1 | ✅ 已讀 VPE StatDef XML |
| `psychic` 旗標無其他隱藏效果 | ✅ `find_usages` 僅 2 處 |
| JobGiver 括號配對 / 符號解析 | ✅ |
| **實際編譯 / 遊戲內測試** | ❌ 皆未執行 |

---
---

# 第四輪：CE 補丁

## 十三、CE 覆蓋率盤點

把模組所有 def 與 `1.6/CE/` 底下的內容對照，未被 CE 觸及的有 11 個。逐一判斷後**其中 9 個確實不需要**（純視覺 def、背景故事、路線 def、記帳 hediff、香草隱身、睡眠系技能）。真正有事的兩個如下。

### `DMST_QuickDraw` — 查過了，CE 下正常，不用補

這個 hediff 給 `ShootingAccuracyPawn -1` 與 `AimingDelayFactor -0.9`。CE 換掉了整套命中模型，我原本預期它會失效。實際查證後兩半都成立：

- **`AimingDelayFactor`**：`CombatExtended.dll` 裡完全沒有這個字串，看起來像廢了。但 `Verb_LaunchProjectileCE.TryStartCastOn` 是先呼叫 `base.TryStartCastOn(...)`，而 `Verse.Verb.TryStartCastOn` 裡就有
  `int ticks = (WarmupTime * CasterPawn.GetStatValue(AimingDelayFactor)).SecondsToTicks();`
  之後 CE 只在 `repeating` 時呼叫 `RecalculateWarmupTicks()`，那個方法只會**再往下乘**一個 <1 的係數，不會重算。**所以 -0.9 照樣生效。**
- **`ShootingAccuracyPawn`**：CE 的 `SwayAmplitude = 4.5 - ShootingAccuracy`，而 `CasterShootingAccuracyValue()` 對 pawn 取的正是 `StatDefOf.ShootingAccuracyPawn`。**-1 等於直接 +1.0 晃動**，在 CE 下這個懲罰甚至比香草更重。

---

## 十四、🔴 CE 下的實質 bug：玩家的召喚砲塔是空彈匣

`DMST_Turret_Sentry` 的 `turretGunDef` 是 `DMS_SubMachineGunMounted`，核心的 CE 補丁給了這把槍 300 發 .22LR 的 `CompAmmoUser`。

`Building_TurretGunCE.SpawnSetup()`：

```csharp
if (!everSpawned && (!Map.IsPlayerHome || Faction != Faction.OfPlayer))
{
    compAmmo?.ResetAmmoCount();
    everSpawned = true;
}
```

自動填彈的條件是「**不在玩家家園地圖** 或 **不屬於玩家**」：

| 情境 | 結果 |
|---|---|
| 敵方軍術士在你的地圖召喚 | `Faction != OfPlayer` → **滿彈**，正常 |
| **玩家的軍術士在自家地圖召喚** | 兩個條件都不成立 → **空彈匣** |

一個靠靈能召喚、12 小時後自毀的臨時砲塔，卻要玩家先搬 .22LR 過去手動裝填才肯開第一槍——這技能在 CE 下等於半殘。

### 修法

新增 `1.6/CE/Defs/Militarmagier_Guns_CE.xml`，定義一把 **CE 專用、刻意不帶 `CompProperties_AmmoUser`** 的 `DMST_Gun_SentryConstruct`，再由 `Turret_CE.xml` 換掉砲塔的 `turretGunDef`。

安全性已確認 — `Building_TurretGunCE` 對沒有彈藥 comp 的槍是 null-safe 的：

```csharp
public bool Reloadable    => CompAmmo?.HasMagazine    ?? false;
public bool EmptyMagazine => CompAmmo?.EmptyMagazine  ?? false;
```

時限仍由 `CompProperties_MechPowerCell` 的 12 小時電池把關，限制放在該放的地方——構裝體的「彈藥」本來就是那顆水晶。**機兵用的那把槍完全沒動**，蹄兔照樣吃它的 .22LR。這也是核心自己的作法（`DMS_Iguana_SubMachineGun`、`DMS_Tarbosaurus_SubMachineGun` 都是為此另開的 CE 專用槍 def）。

---

## 十五、`DMST_FieldAid` 射程

CE 下所有技能射程都被拉長了（強光 10.9→15、換位 18.9→24、兩個召喚 8.9→12、解體 10.9→17.5），**只有戰地急救的 3.9 被漏掉**。在 CE 的交戰距離下那等於要站進把隊友打倒的那片火網裡。已補 `3.9 → 6`。

---

## 十六、確認過但不需要動的

- **石像鬼的 `CE_SMG` 標籤**：核心也在用，是有效的 CE 標籤。
- **`CompInventory`**：核心的 CE 補丁完全沒提它，CE 自己會補到所有 pawn 上。
- **`LoadoutPropertiesExtension`（石像鬼）**：它的 PawnKindDef 沒有 `weaponTags`/`weaponMoney`，出生就沒武器、由玩家自行裝配，不需要出生彈匣設定。
- **裝備護甲**：既有的 `StuffEffectMultiplierArmor` / `Bulk` / `WornBulk` 已足夠。

---

## 十七、第四輪驗證狀態

| 驗證 | 結果 |
|---|---|
| 全部 XML well-formed | ✅ |
| `1.6/CE` 由 LoadFolders 以 `IfModActive` 掛載，`Defs/` 與 `Patches/` 都會載入 | ✅ |
| CE 補丁所有 `defName="DMST_*"` xpath 目標都存在 | ✅ |
| `turretGunDef` 指向的新 def 存在 | ✅ |
| `Building_TurretGunCE` 對無彈藥 comp 為 null-safe | ✅ 已反編譯確認 |
| `AimingDelayFactor` / `ShootingAccuracyPawn` 在 CE 下仍生效 | ✅ 已反編譯確認 |
| **遊戲內測試（尤其 CE 環境）** | ❌ 未執行 |

> ⚠️ 這輪最需要實測的是砲塔：在 **CE + 玩家自家地圖**召喚一座，確認它不用裝彈就會開火、且 12 小時後照常停機；順帶確認敵方召喚的那座也還正常。

---
---

# 第五輪：Pawnkind 變體與背景故事

## 十八、🔴 先修一個 bug：背景故事從來沒出現過

`DMST_BaseBackstory` 宣告 `spawnCategories: DMST_Magier`，但 `DMST_Taktikmagier` 從 `DMS_SoldierBase` 繼承來的是

```xml
<backstoryCategories>
  <li>FleetChild</li>
  <li>Mili_Standard</li>
  <li>Mili_Expert</li>
</backstoryCategories>
```

**沒有任何 pawnkind 引用 `DMST_Magier`**，所以那個類別是孤兒——原有的 1 個、以及我第一輪補的 2 個背景故事，從頭到尾都不曾被抽到過。

修法：新的 `DMST_MagierBase` 明確宣告 `backstoryCategories Inherit="False"` = `DMST_Magier` + `FleetChild`，並且**童年與成年兩個 slot 都自備**，讓類別能獨立運作；`FleetChild` 保留在旁邊提供變化。

---

## 十九、三個階層變體

抽出 `DMST_MagierBase` 收攏共用設定，底下三階。分野是「爬到樹的哪一層」，不是「做什麼」：

| Pawnkind | 戰力 | initialLevel | unlockedPaths | 優化點數 | 頭銜 | 隱身 |
|---|---|---|---|---|---|---|
| `DMST_Magier_Adept` 見習 | 70 | 2 | `1~2\|1~3` | 0~2 | Corporal | ❌ |
| `DMST_Taktikmagier` 戰術 | 100 | 4 | `1~4\|2~10` | 2~6 | WarrantOfficer | ✅ |
| `DMST_Magier_Veteran` 資深 | 140 | 6 | `1~5\|6~14` | 4~9 | Lieutenant | ✅ |

幾個刻意的設計點：

- **只有資深階能碰到樹的第 5 層**，也就是只有它可能帶強制清醒與解體。`unlockedPaths` 的語法是 VPE 的 `PathUnlockData`：`<等級範圍>|<技能數量>`，而這條路線有 5 層——原本的 `1~4` 意味著**正式階永遠抽不到那兩個技能**，這點之前沒被寫下來過，現在註解在 def 裡了。
- **見習階拿不到軍術士斗篷**，因此完全沒有隱身（技能由斗篷授予、又被頭盔+斗篷套裝限制擋著）。這裡有個坑：基底的 `apparelTags: DMST_Magier` 會讓 `apparelMoney` 有機會替她隨機滾出一件斗篷，等於繞過設計。所以見習階改用 `apparelTags Inherit="False"` = `DMS_Garrison`，穿標準駐防裝加上必備的軍術士頭盔。
- `DMST_Taktikmagier` 的 defName 保持不變，既有的 PawnGroup 與 CE 補丁引用不會斷。

襲擊組的權重刻意頭重腳輕：見習 2 / 正式 1 / 資深 0.35。商隊護衛只出正式階。

---

## 二十、14 個背景故事

5 童年 + 9 成年，全部掛在 `DMST_Magier` 類別下。取材自模組既有的設定文本：阿尼瑪主星、火藥武器摧毀施法者血脈、「不追求致命奇蹟而是精煉基礎法術」、殖民軍團的 AI 統領與軍事精英制。

**童年**：避難所孩童、血脈受監護者、靶場跑腿、預備役施法者、殘骸拾荒者。

**成年**（每一個對應一項招牌技能）：

| 背景 | 對應技能 |
|---|---|
| 退役醫務兵 | 戰地急救 |
| 戰列軍術士 | 路線主軸（閃光＋點放） |
| 突擊工兵 | 解體 |
| 破門軍術士 | 突擊閃光 |
| 構裝體匠人 | 兩個召喚 |
| 反機械專員 | 電磁脈衝 |
| 長哨 | 強制清醒 |
| 光學迷彩斥候 | 靈能迷彩 |
| 隨扈軍術士 | 換位 |

繁體中文 DefInjected 已同步（含三個 pawnkind 標籤）。

---

## 二十一、第五輪驗證狀態

| 驗證 | 結果 |
|---|---|
| 全部 XML well-formed | ✅ |
| 14 個背景故事，5 童年 / 9 成年 | ✅ |
| 英文 defName 與繁中翻譯鍵一一對應 | ✅ 14/14 |
| 三個 pawnkind 在 PawnGroup 與 CE 補丁中皆有引用 | ✅ |
| 外部引用（`DMS_SoldierBase`、`DMS_Corporal`/`DMS_Lieutenant`、`DMS_Garrison`、`FleetChild`）皆存在 | ✅ |
| `minGenerationAge` 為合法 PawnKindDef 欄位 | ✅ 已反編譯確認 |
| **遊戲內測試** | ❌ 未執行 |

> ⚠️ 值得留意：`[PAWN_objective]` 這個 token 用在兩個背景故事裡（突擊工兵、隨扈軍術士）。它是合法的，但如果生成出來的句子讀起來怪怪的，那兩處是第一個要看的地方。

---
---

# 第六輪：以繁中文本為準回頭校正英文

## 二十二、🔴 翻譯檔的 XML 宣告壞掉了

手改後的 `Languages/ChineseTraditional/DefInjected/BackstoryDef/BackstoriesDefs.xml` 第一行是：

```
 <?Xml version="1.0" encoding="utf-8"?>
```

兩個問題疊在一起：

1. `<?` 前面多了一個**半形空格**——XML 宣告前不允許任何字元
2. `Xml` 的 **X 是大寫**——宣告必須是小寫 `xml`

實測 `xml.parsers.expat` 直接報 `not well-formed (invalid token): line 1, column 6`。RimWorld 會整個檔案載入失敗，**14 個背景故事會全部顯示英文**，而且未必會有明顯的錯誤提示。已修正為 `<?xml ...`（同時去掉前導空格），現在可正常解析。

> 這類問題不會在遊戲裡「壞得很明顯」，只會安靜地退回英文。之後手改翻譯檔後值得順手驗一下能不能解析。

---

## 二十三、四則以中文為準回寫英文

你改動的是中文，所以這一輪**把中文當原文、英文當譯文**回頭改，而不是反過來。四則有實質差異：

| 背景 | 改動 |
|---|---|
| **血脈受監護者** | 撫養者從「國家」改成「**法師塔**」；刪掉「天賦太稀有，不能留在砲彈打得到的地方」那句，改為僅存親人被**遷移安置到各處** |
| **預備役施法者** | 整條重寫。原本是「神學院落選生」，現在是**天外人佔領的城邦**出身、因靈能親和性優秀而**全額補助學費**、畢業即**預備役士官** |
| **退役醫務兵** | 精簡，拿掉「用每個軍術士都被操練過的基礎施法在火線下封閉創口」 |
| **戰列軍術士** | 重寫為「與**同齡的戰術施法者們**為服務的**國家**獻出鮮血」，拿掉「一次閃光加上一串點放勝過任何法術」 |

順帶調整：預備役施法者的 `skillGains` 原本是 `Intellectual 4 / Artistic -3`——那個 -3 是在嘲諷神學院導師要的「藝術性大法術」，新設定裡沒有對應物了，改成 `Intellectual 4 / Social 3`（受補助教育＋士官身分）。

### 兩個新的設定詞

中文引入了模組其他地方沒出現過的兩個概念，已寫進 def 檔頂端的註解，避免日後新增背景故事時各寫各的：

- **法師塔**（Mage Tower）— 登記倖存血脈並出資撫養的機構。英文用 "the Mage Tower"。
- **天外人**（offworlders）— 佔領城邦的一方。核心模組的繁中檔案裡查無此詞，是這個模組自己的用語；英文用 "offworlders"，避開 RimWorld 官方的 Outlander（邊緣人）以免混淆。

def 檔頂端也加了一行提醒：這四則以繁中為準，英文是回寫的，日後要一起改。

### 一個副作用，供你判斷

原本「神學院落選生」是童年組裡唯一直接呼應路線描述（「不追求奇蹟，只反覆精煉基礎法術」）的一則。換成預備役施法者之後，那個主題在童年組就沒有著落了，只剩成年組的破門軍術士還帶著「閃光→兩步→開槍」的教範味道。如果你想把主題找回來，最省事的是在成年組再補一則，而不是動已經定稿的童年組。

## 二十四、第六輪驗證

| 驗證 | 結果 |
|---|---|
| 翻譯檔可正常解析 | ✅ 已修正宣告 |
| 全部 XML well-formed | ✅ |
| 14 則 × 3 個欄位（title/titleShort/description）中文鍵齊全 | ✅ 無缺漏、無孤兒鍵 |
| 英文 def 與中文語意一致 | ✅ 四則已回寫 |

---
---

# 第七輪：補上阿尼瑪主星與殖民艦隊的因果

## 二十五、這條設定補上了三個原本說不通的地方

你給的設定是：**阿尼瑪主星曾與 DMS 本體的殖民艦隊對抗，戰後倖存的靈能者血脈成為艦隊的一份子。**

這一句同時解掉了先前文本裡三個懸空的環節：

1. **火藥是誰的。** 路線描述原本只寫「先進的火藥武器摧毀了無數施法者血脈」，沒說是誰帶來的。現在有了對象。
2. **為什麼軍術士會出現在 `DMS_Army` 裡。** 這個模組把 `DMST_Taktikmagier` 塞進武裝殖民艦隊的襲擊組，但先前沒有任何文本解釋一個靈能傳統為何會在艦隊編制內。「不是被消滅而是被吸收」正好接上核心設定裡「軍事精英制、只要願意打就有位置」那句。
3. **「天外人」是誰。** 你在預備役施法者裡寫的「天外人佔領的一處城邦」，現在確定就是艦隊——從阿尼瑪主星本地人的視角看過去的稱呼。

## 二十六、改了哪裡

**`DMST_Militarmagier` 路線描述**（英文 def + 繁中 DefInjected 同步）從兩段擴成三段，中間插入戰敗與吸收：

> 當武裝殖民艦隊為它而來時，阿尼瑪主星選擇了抵抗——然後親身學到了先進的火藥武器會對一個建立在宏大法術之上的傳統做什麼。一條又一條才華洋溢的施法者血脈，被消耗在為那些總是來得太遲的引導爭取時間上。
>
> 那些血脈剩下的部分與其說是被征服，不如說是被吸收。艦隊是個精英制的組織，並不太在乎一名堪用的士兵從哪裡來；阿尼瑪主星的施法者們接下了委任狀，而他們的後代如今在當年擊垮自己的那面旗幟下服役。

「血脈被消耗在為來得太遲的引導爭取時間上」這句是刻意寫的——它同時解釋了為什麼軍術士這條路線會走向「精煉基礎法術、提高頻率」而不是追求大法術。原本這個轉折是憑空宣稱的，現在有了代價。

**術語對齊**：核心把 `DMS_Army` 譯為**武裝殖民艦隊**，所以繁中一律用「殖民艦隊」，英文用 "Colonization Fleet"。

**`BackstoriesDefs.xml` 頂端的設定註解**補上了對抗與吸收這條線，並註明「天外人 = 艦隊，但刻意保留為地面視角的稱呼，關聯在路線描述裡講明」——避免日後有人「順手統一術語」把那個味道改掉。

## 二十七、既有背景故事的重讀

沒有任何一則需要改寫，但有幾則的意思變深了，記錄一下：

| 背景 | 新讀法 |
|---|---|
| 避難所孩童 | 那個「按時震動的天花板」現在有了明確的來源 |
| 血脈受監護者 | 法師塔的造冊與撫養，讀起來像是戰後清點殘存資產 |
| 預備役施法者 | 佔領方出錢供你上學、再把你收編成預備役士官——吸收機制的具體樣貌 |
| 戰列軍術士 | 「為服務的國家獻出鮮血」保持模糊是對的，那個「國家」現在可以是任一邊 |

## 二十八、第七輪驗證

| 驗證 | 結果 |
|---|---|
| 全部 XML well-formed | ✅ |
| 路線描述 EN / zh 的 `\n` 段落標記數量一致 | ✅ 4 / 4 |
| 翻譯鍵無真實重複 | ✅ 先前掃出的 5 筆全是跨 scope（不同 Def 型別資料夾、或不同語言的 Keyed），RimWorld 分開索引，屬正常 |
| 術語與核心一致（武裝殖民艦隊） | ✅ 已比對核心 zh-TW FactionDef |
