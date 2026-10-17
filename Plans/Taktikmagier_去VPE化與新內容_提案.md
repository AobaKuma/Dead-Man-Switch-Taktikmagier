# Taktikmagier 去 VPE 化與新內容提案

更新日期：2026-10-17
目標版本：Taktikmagier 2.0／DMS 2.0.3／RimWorld 1.7（FFF 1.7）
文件狀態：**提案，尚未實作**。第 2 節標「已查證」者為對照 1.7 `Assembly-CSharp`、FFF 1.7 `Fortified.dll` 與 DMS 2.0.3 XML 的結果。第 3、4 節的數值都是初稿，需要實測平衡。需要你拍板的地方集中在第 8 節（D1–D8）。

## 1. 摘要

1. **連 VEF 一起移除**。DMS 2.0.3 本體只依賴 Harmony、Biotech、FFF，已經不依賴 VEF；Taktikmagier 目前對 VEF 的依賴完全是 VPE 帶進來的。
   - **更正（2026-10-17）**：初稿寫「兩者都只有 1.6 版」，這是照抄 `.csproj` 的舊註解，沒有實際查證。創意工坊資料夾裡 VPE 與 VEF 都已經有 1.7 版（VPE 的 1.7 DLL 於 2026-10-16 更新，VEF 於 2026-10-13）。所以移除它們的理由是減少依賴（使用者的需求），不是 1.7 不能用；「保留 VPE、改用它的 1.7 DLL」也是可行的替代方案。`Ability_Flash.cs` 的反射繞道是在建置時參考了 1.6 DLL 才需要的。
2. **改用原版心靈系統**（皇權 DLC）：心靈連結等級取代 VPE 的路徑等級，神經熱／心靈專注沿用原版。原版 `Psycast` 本身就要求皇權（2.3），所以皇權會變成硬依賴。VPE 本來就需要皇權，現有玩家不受影響。
3. **用「教範＋精煉」取代 VPE 的點數技能樹**，這是本提案的核心。路徑描述寫的是軍術士「不追求致命的大法術，而是持續精煉基礎術式，提升效率與頻率」。所以：
   - 法術從「軍術教範」學會（原版書本系統，2.3）。
   - 每個法術有**基礎 I → 熟練 II → 精通 III** 三階，靠實戰施放累積；精通時解鎖該法術專屬的「精通特性」。
   - 軍術士學的法術不多，但每一個都練到極致。設定直接變成玩法。
4. 新內容：5 個新法術（含「架勢」機制）、軍術教範、專注安瓿，以及能從法術階級看出強弱的敵方軍術士。
5. 改完後的依賴：Harmony、Royalty、Biotech（本體需要）、FFF、DMS Core。

## 2. 已確認的現況

### 2.1 VPE／VEF 依賴盤點（已查證，本模組原始碼）

| VPE／VEF 提供的東西 | 用在哪裡 | 替代方案 |
|---|---|---|
| `VanillaPsycastsExpanded.PsycasterPathDef`、`AbilityExtension_Psycast`（path／level／order、專注、熱、`entropyGainStatFactors`） | 全部 10 個法術、路徑定義 | 原版 `AbilityDef` + `Psycast`；等級 = 心靈連結等級；消耗用 `statBases` 的 `Ability_EntropyGain`／`Ability_PsyfocusCost`（3.2） |
| VPE 等級／點數成長、`VPE_PsyfocusCostFactor` | 法術成本縮放 | 精煉階級（3.4） |
| `PawnKindAbilityExtension_Psycasts`、`VPE_PsycastAbilityImplant` | 三階敵方軍術士 | 自訂 `MagierKindExtension` + 產生後補上心靈連結（3.7） |
| `VEF.Abilities.Ability` 子類別、`CompAbilities`、`AbilityExtension_AbilityMod` | `Ability_Flash`／`Repel`／`Swap`，以及 Construct、Heal、Calm、Destroy 等 extension | 全部改寫成原版 `CompAbilityEffect`（5.2） |
| `AbilityExtension_Explosion`、`FleckOnTarget`、`EffecterOnTarget`、`Hediff` | 排斥、電磁脈衝、組裝構裝體、快速拔槍 | 原版 `CompProperties_AbilityExplosion`、`CompProperties_AbilityFleckOnTarget`、`CompProperties_AbilityEffecterOnTarget`、`CompProperties_AbilityGiveHediff` |
| `VFEA_UseAbility` job、`TryGetAttackVerb` 後置的 AI 施法 | `JobGiver_TaktikmagierCombat` | 原版 `pawn.abilities.GetAbility()` → `CanCast` → `GetJob()`（2.3） |
| `VPE_ConstructBase`、`Pawn_Construct`、`IMinHeatGiver`、`CompBreakLink`、`DeathActionWorker_SlagChunk`、`VPE_SteelConstructFlesh`、`VPE_Filth_Metal`、`VPE_NoPain`、`VPE_Pawn_SteelConstruct_Angry` | 石像鬼、哨戒砲塔 | 石像鬼改用 FFF 無人機架構（3.6）；連結與維持改由自己的 comp／hediff 處理 |
| `VPE_PsychicEntropyMinimum`（VPE 的屬性） | `Hediff_Focus` | 改為佔用 `PsychicEntropyMax`（3.6） |
| VEF `AnimalBehaviours`（`InitialHediff`、`Draftable`、`DiseaseImmunity`、`DiseaseEventImmunity`） | 石像鬼 | 改成機械體後就不需要了（3.6） |
| `VPE_Eltex`（`MayRequire`） | 頭盔、斗篷配方 | 換成 DMS／原版材料（2.4） |
| `DefInjected/VEF.Abilities.AbilityDef`、`VanillaPsycastsExpanded.PsycasterPathDef` | 簡中、繁中翻譯 | 改名為 `DefInjected/AbilityDef`，刪掉路徑那份 |
| CE 修補裡的 `VEF.Abilities.AbilityDef` xpath | `1.6/CE/Patches/Militarmagier_CE.xml` | 重寫（5.5） |

不受影響的部分：`DMST_Camouflage` 本來就是原版 `AbilityDef`，`CompAbilityEffect_RequireApparel` 本來就是原版 `AbilityComp`。兩者照舊。

### 2.2 版本現況（已查證）

- DMS 2.0.3 的 `supportedVersions` 只有 1.7，內容都放在 `1.7/`。
- Taktikmagier 目前在 1.7 下載入的是 `/` + `1.6` + `1.7/Assemblies`；`.csproj` 同時建置 `net48` 與 `net10.0`，兩個目標都參考 VPE／VEF 的 1.6 DLL（雖然創意工坊已經有它們的 1.7 版，見第 1 節的更正）。
- VPE 的 About.xml 要求 `Ludeon.RimWorld.Royalty`（已查證），所以現有玩家都有皇權，D1 的前提成立。
- 移除 VPE／VEF 之後，`net10.0` 單一目標就夠了，也不需要 `RW_1_7` 反射繞道。

### 2.3 原版與 FFF 可用元件（已查證）

心靈系統（1.7 `Assembly-CSharp`）：

- `Psycast.Activate` 開頭呼叫 `ModLister.CheckRoyalty("Psycast")`，`GetGizmos` 只在 `RoyaltyInstalled` 時產生 `Command_Psycast`。**沒有皇權就無法施放原版心靈技能。**
- `Pawn_PsychicEntropyTracker`：神經熱的累加與衰退不需要心靈連結，但只要有熱就會加上 `HediffDefOf.PsychicEntropy`，而這個 hediff 定義在 `Royalty/Defs`。心靈專注只有在 `Psylink != null` 時才會隨時間下降（`NeedsPsyfocus`）。**更正**：初稿寫「冥想累積也需要心靈連結」是沒有查證的推論；`MeditationUtility.CanMeditateNow` 其實不檢查心靈連結。結論不變：法術等級綁心靈連結（`GizmoDisabled`），神經熱 hediff 只在皇權裡，所以一定要有皇權，也一定要有心靈連結。
- `Psycast.GizmoDisabled`：`def.level > 0` 時要求心靈連結等級 ≥ `def.level`；同時依專注區間限制可施放等級（`MaxAbilityLevelPerPsyfocusBand` = 2／4／6）。成本直接讀 `def.EntropyGain`／`def.PsyfocusCost`，屬於 def 層級。
- `AbilityDef.IsPsycast => typeof(Psycast).IsAssignableFrom(abilityClass)`。
  - `Hediff_Psylink.TryGiveAbilityOfLevel` 會從**所有** `IsPsycast` 的法術隨機挑一個發給升級者。
  - `ThingDefGenerator_Neurotrainer` 會替**每一個** `IsPsycast` 法術產生一個 `Psytrainer_<defName>` 物品，帶 `RewardStandardLowFreq` 標籤與 `ExoticMisc` 交易標籤。
  - 也就是說，不處理的話，軍術士法術會被隨機發給一般心靈施法者，也會以心靈訓練器的形式出現在戰利品與商人那裡（D3）。
- 數值尺度：`PsychicEntropyMax` 基礎值 30，乘上心靈連結階段係數（1 級 1.0 → 6 級 2.6667）與心靈敏感度。戴滿頭盔與斗篷（敏感度 +0.2）的 1 級施法者上限 36。現在突擊閃光 25 熱，會吃掉 69%，所以**所有熱值都要重算**（3.2）。原版 1 級法術大約 8–12 熱。

AI、書本、物品：

- `JobGiver_AICastAbility`／`JobGiver_AICastAbilityOnSelf`／`JobGiver_AIAbilityFight` 與 `Ability.GetJob(target, dest)` 是原版的 AI 施法路徑，不需要 VEF 那套先把 `currentlyCasting` 塞進 comp 的做法。
- `PawnKindDef.abilities`：產生時固定給的能力。
- `BookOutcomeDoer`（`DoesProvidesOutcome`、`OnBookGenerated`、`OnReadingTick(reader, factor)`、`GetBenefitsString`、品質）：`BookOutcomeDoerGainSkillExp` 就是「品質決定上限」的現成範例。書本需要 `CompProperties_Book`，加上單張、`openGraphic`（Multi）、`verticalGraphic`（Multi）三組貼圖。
- `CompAbilityEffect_Explosion`：**固定在施法者位置爆炸**，而且沒有忽略清單（施法者自己也在範圍內）。`IngestionOutcomeDoer_OffsetPsyfocus` 可以直接拿來做專注藥劑。

FFF 1.7：

- `CompDrone` + `Patch_CanDraftMech`：`SetPlatform(pawn)` 之後，無人機只有在控制者已徵召、在同一張地圖、而且由玩家控制時才能徵召。這正好就是「軍術士完全掌控它」。
- `WeaponUsableMech`：可裝備武器的機械體，取代 `Pawn_ConstructWeaponUsable` 與 `IWeaponUsable`。DMS 的無人機 `DMS_BaseDroidRace`（`BaseMechanoid` + `Fortified.WeaponUsableMech` + `CompProperties_Drone`）就是這個組合。
- `HediffComp_ProtectiveShield`（帶 hitpoints 的 hediff 護盾）、`CompAbilityEffect_DirectionalExplosion`（扇形）。

### 2.4 盤點時順便發現的問題

- 斗篷的 `<PsychicEntropyMax>0.75</PsychicEntropyMax>` 是 +0.75 點，在原版尺度下幾乎沒有效果。建議改成 +8～10。
- `VPE_Eltex` 移除後，頭盔與斗篷的配方會直接變便宜。建議換成 `ComponentIndustrial` 或 DMS 材料。
- `Textures/UI/AbilityIcon/` 的 10 張圖和 `Textures/Abilities/Militarmagier/` 完全重複，而且沒有任何 Def 引用，可以刪掉。

## 3. 核心設計：軍術士教範（取代 VPE 心靈路徑）

### 3.1 設定依據

阿尼瑪原星的施法者原本靠血脈口耳相傳。被殖民艦隊吸收之後，法師塔把殘存血脈的術式拆解、標準化，印成**軍術教範**（Feldhandbuch）。這是艦隊式的魔法：誰讀了教範、誰在戰場上練得熟，誰就是軍術士。這與 DMS 的「任人唯才」也對得上。

玩法上要傳達的是：**不是學更多法術，而是把同一招練得更好。**

### 3.2 施法基礎：原版心靈技能

- 所有軍術士法術都是 `abilityClass` = `Psycast` 的 `AbilityDef`。成本與冷卻全部寫在 def 上，所以原版 gizmo、提示、專注區間限制、AI 都能直接正確運作。
- 路徑等級 1–5 直接對應心靈連結等級。
- 熱值初稿（I 階；專注消耗沿用目前數值，它本來就是原版尺度）：

| 法術 | 等級 | 神經熱 現行 → 提案 | 專注 | 冷卻 |
|---|---|---|---|---|
| 突擊閃光 | 1 | 25 → 12 | 5% | 60 秒 |
| 排斥 | 1 | 25 → 12 | 4% | 60 秒 |
| 換位 | 2 | 10 → 8 | 2% | 20 秒 |
| 組裝砲塔構裝體 | 3 | 5 → 5，另佔用 10 | 1% | 20 秒 |
| 組裝石像鬼構裝體 | 3 | 5 → 5，另佔用 12 | 1% | 20 秒 |
| 快速拔槍 | 3 | 10 → 6 | 1% | 10 秒 |
| 電磁脈衝 | 4 | 10 → 20 | 1% | 60 秒 |
| 戰地急救 | 4 | 20 → 15 | 4% | 10 秒 |
| 強制清醒 | 5 | 14 → 10 | 4% | 20 秒 |
| 解體 | 5 | 10 → 15 | 4% | 20 秒 |

電磁脈衝調高的理由：半徑 14.9 格、40 點 EMP，在原版尺度下 10 熱太便宜。

### 3.3 學習：軍術教範（原版書本）

- 新增 `DMST_FieldManual`（Book）與 `BookOutcomeDoer_MagierManual`。
  - `OnBookGenerated`：從教範法術中挑一個，低等級較常見。
  - `OnReadingTick`：累積讀者對該法術的學習進度。
- 讀完時：
  - 讀者心靈連結等級夠，而且還沒學過 → 學會該法術的 I 階，並發信件通知。
  - 已經學會 → 改成累積精煉值（3.4），**最多到 II 階**。
  - 心靈連結等級不夠 → `DoesProvidesOutcome` 回傳 false，`GetBenefitsString` 顯示「需要 N 級心靈連結」。
- 品質影響學習速度（約 ×0.6～×1.6，實作時對照 `BookUtility` 的曲線）。
- 取得途徑：
  - 艦隊商隊或商人販售 1～2 本（1～3 級）。
  - 資深軍術士身上帶著，約 30% 機率，內容是她會的法術。
  - 任務獎勵。
  - 「用 DMS 印表機複印教範」列為延伸（4.4），因為要先查證配方產出的 Book 會不會觸發 `OnBookGenerated`。
- 書名靠 `RulePackDef` 產生，例如「《軍術教範：突擊閃光》第 3 版」；描述加一句「艦隊財產，嚴禁外借」。

### 3.4 精煉：基礎 I → 熟練 II → 精通 III

**做法：每階都是獨立的 AbilityDef，升階時把舊的換成新的。**

```xml
<AbilityDef Name="DMST_FlashBase" ParentName="DMST_MagierSpellBase" Abstract="True"> … 共用的目標與效果 … </AbilityDef>
<AbilityDef ParentName="DMST_FlashBase"><defName>DMST_Flash</defName>    … 熱 12，冷卻 3600 … </AbilityDef>
<AbilityDef ParentName="DMST_FlashBase"><defName>DMST_Flash_II</defName> … 熱 9，冷卻 2700 … </AbilityDef>
<AbilityDef ParentName="DMST_FlashBase"><defName>DMST_Flash_III</defName> … 熱 6，冷卻 1800，加上精通特性 comp … </AbilityDef>
```

這樣做的好處：

- 成本是 def 層級的值，原版的 `CanCast`、`GizmoDisabled`、`Command_Psycast` 提示、AI 全部都不用 override 就是對的。
- 精通特性只是 III 階多掛的 comp 或不同的參數，用 XML 就能表達。
- 譯名不必寫三份：II／III 的 label 在 `StaticConstructorOnStartup` 由 I 階的 label 加上「 II／III」自動產生，精通特性的說明放在 Keyed。
- CE 修補可以直接打在抽象基底（`@Name="DMST_FlashBase"`）上：修補在繼承解析之前套用，所以三階會一起改到。

數值初稿：

| 階級 | 神經熱 | 冷卻 | 其他 |
|---|---|---|---|
| I 基礎 | ×1 | ×1 | — |
| II 熟練 | ×0.75 | ×0.75 | — |
| III 精通 | ×0.5 | ×0.5 | 精通特性（3.5） |

精煉值來源：

| 來源 | 精煉值 | 上限 |
|---|---|---|
| 實戰施放（目標敵對，或 25 格內有敵人） | +1 | 無 |
| 非戰鬥施放 | +0.2 | 只到 II |
| 閱讀同一法術的教範 | 依品質，每小時 +N | 只到 II |

門檻：II = 20、III = 60。**精通只能在戰場上取得**（D4）。

紀錄與換階：

- 新增永久 hediff `DMST_MagierDoctrine`，健康頁顯示為「軍術士資歷」。它儲存每個法術家族的精煉值，描述中列出各法術的階級與距離下一階的進度。
- 每個法術都掛一個 `CompAbilityEffect_Drill`，在 `Apply` 時加精煉值。
- 換階**不在施法當下進行**：先標記，等 hediff 下次 tick、而且施法者沒在施法時，才 `RemoveAbility` 舊階、`GainAbility` 新階，並把剩餘冷卻沿用到新階。

### 3.5 精通特性

| 法術 | III 階精通特性 | 實作 |
|---|---|---|
| 突擊閃光 | 殘影：被擊暈的目標醒來後 10 秒內射擊精度 −50%（機械體為「感測器殘像」） | 額外給一個 hediff |
| 排斥 | 破陣：被推出去的目標若中途撞牆，受到 5～10 點鈍傷 | `Repel` 在提前停下時加傷害 |
| 換位 | 瞬換：施法時間 2 秒 → 0 | 參數 |
| 組裝砲塔構裝體 | 強化框架：砲塔耐久 ×2、電池 ×1.5、佔用 −30% | 參數 |
| 組裝石像鬼構裝體 | 核心結晶：石像鬼被摧毀時退回沖壓零件 | 死亡處理 |
| 快速拔槍 | 槍鬥術：精度懲罰 −1 → −0.5，持續時間 +50% | 另一個 hediff |
| 電磁脈衝 | 敵我識別：己方機械體與自己的構裝體不受影響（I、II 階無差別） | 自訂爆炸 comp（帶忽略清單） |
| 戰地急救 | 止血帶：完全止血，並給 30 秒止痛 | 額外 hediff |
| 強制清醒 | 戰時樂觀：可以對其他人施放（I、II 階只能對自己） | 目標參數 |
| 解體 | 精準拆解：可瓦解的耐久門檻 800 → 1200，殘餘耐久係數 0.25 → 0.1 | 參數 |
| 標定（新） | 連鎖標定：同時標定目標周圍 3 格內最多 3 名敵人 | 參數 |
| 精準瞄準（新） | 穩定：不再增加瞄準時間 | 另一個 hediff |
| 煙幕（新） | 範圍 +50%，持續時間加倍 | 參數 |
| 戰地重構（新） | 修復時也補充砲塔電池與構裝體的動力 | 參數 |
| 術式護壁（新） | 範圍化：同時保護 2 格內的所有友軍 | 參數 |

電磁脈衝的敵我識別是和 DMS 本體最有綜效的一項：DMS 玩家通常帶著自己的機械體，I 階時施法者得注意站位。

### 3.6 構裝體維持

- **佔用熱上限，取代 VPE 的最低熱下限。** `DMST_MagierDoctrine` 合併現在 `Hediff_Focus` 的工作：每個存活的構裝體依 `ConstructHeatExtension.heat` 降低 `PsychicEntropyMax`。效果和「最低熱」相同（可用空間變少），但只用到原版屬性。
  - `heat` 的意義改為「佔用量」，數值改成 10／12（3.2）。
  - 召喚前在 `GizmoDisabled` 檢查「上限 − 佔用 − 目前熱值 ≥ 本次消耗」，避免一召喚就超載。
- **解除聯結**：新增 `CompMagierLink`，記錄召喚者、提供「解除術式聯結」gizmo（需要新貼圖，第 6 節），並在召喚者死亡或倒地時讓構裝體崩解。
- **石像鬼改用 FFF 無人機架構**，比照 `DMS_BaseDroidRace`：
  - `thingClass` = `Fortified.WeaponUsableMech`。
  - `CompProperties_Drone`，召喚時 `SetPlatform(召喚者)`。這樣只有召喚的軍術士徵召時，石像鬼才能徵召。
  - 原有的 `Fortified.MechWeaponExtension`、`TurretMannableExtension` 照舊。
  - 機械體本來就不會痛、不會生病，VEF 那幾個 comp 都可以刪掉。
  - 死亡改為留下 `ChunkSlagSteel`，血改用原版 `Filth_MachineBits`。
  - 副作用：石像鬼會被 EMP 影響（包括自己的電磁脈衝），這正好讓 3.5 的敵我識別有意義（D7）。
- 哨戒砲塔：把 `CompProperties_BreakLink` 換成 `CompMagierLink`，其餘不動。

### 3.7 敵方軍術士

- `MagierKindExtension`（DefModExtension）取代 `PawnKindAbilityExtension_Psycasts`，由一個 `PawnGenerator.GeneratePawn` postfix 讀取：裝上 `PsychicAmplifier` 到指定等級，再從等級範圍內抽法術，依機率給 II／III 階。

```xml
<li Class="Militarmagier.MagierKindExtension">
  <psylinkLevelRange>3~4</psylinkLevelRange>
  <spellLevelRange>1~4</spellLevelRange>
  <spellCountRange>2~5</spellCountRange>
  <tierChances><li>0.7</li><li>0.25</li><li>0.05</li></tierChances>
</li>
```

| 兵種 | 心靈連結 | 法術等級 | 法術數 | 階級傾向 |
|---|---|---|---|---|
| 見習軍術士 | 2 | 1～2 | 1～3 | 幾乎都是 I |
| 戰術軍術士 | 3～4 | 1～4 | 2～5 | I 為主，少量 II |
| 資深軍術士 | 5～6 | 1～5 | 5～8 | II 為主，可能有 III；30% 帶教範 |

- 這樣**強弱看得出來**：玩家選取敵人時會看到「突擊閃光 III」，就知道這個要先處理。
- `JobGiver_TaktikmagierCombat` 改用原版 API，以法術家族（3.4）查找目前的階級，再加上新法術的判斷：
  - 標定：目標隱形，或是威脅最高的敵人。
  - 精準瞄準／快速拔槍：依敵人距離切換架勢。
  - 術式護壁：射程內有友軍生命值低於 50%。
  - 戰地重構：有友方機械體或構裝體耐久低於 50%。
  - 煙幕：自己生命值低於 60%，而且正被射擊。

## 4. 新內容

### 4.1 新法術（第一批）

| 法術 | 等級 | 目標 | 效果（I 階） | 熱／專注／冷卻 | 實作 |
|---|---|---|---|---|---|
| 標定 Markieren | 1 | 敵人 | 20 秒：解除並阻止心靈隱形、受到傷害 +15%、閃避 −10% | 6／1%／20 秒 | `GiveHediff` + 移除隱形的小 comp |
| 精準瞄準 Zielhilfe | 2 | 自己 | 30 秒：射擊精度 +，瞄準時間 +25%。與快速拔槍互斥 | 6／1%／10 秒 | `GiveHediff` + 互斥 comp |
| 煙幕 Nebel | 2 | 地點 | 目標點 3.9 格施放致盲煙霧（`GasType.BlindSmoke`） | 10／1%／30 秒 | 在目標點爆炸的自訂 comp（原版的固定在施法者身上） |
| 戰地重構 Instandsetzung | 3 | 機械體、構裝體、建築 | 修復建築耐久，或治療機械體傷勢（總量 40） | 12／2%／10 秒 | 自訂 comp，是戰地急救的機械版 |
| 術式護壁 Schild | 4 | 友軍 | 30 秒內吸收 60 點傷害 | 15／2%／30 秒 | FFF `HediffComp_ProtectiveShield`（**待查證**：它實作了 FFF 改裝系統的 `IModificationMergeCapacityProvider`，要確認能不能單獨掛在會消失的 hediff 上；不行就自己寫一個吸收傷害的 HediffComp） |

設計考量：

- **標定**是對付敵方軍術士隱形斗篷的反制手段。目前玩家除了 DMS 本體的建築以外沒有別的辦法。
- **戰地重構**直接接上 DMS 的主題：軍術士在野外維修艦隊的機械體，也能修自己的砲塔和石像鬼。這是石像鬼背景「構裝師 Construct Wright」的另一半。
- 加上第一批以後總共 15 個法術，每一級都有 2～4 個：1 級 3、2 級 3、3 級 4、4 級 3、5 級 2。

### 4.2 架勢：快速拔槍 ⇄ 精準瞄準

兩者互斥，同一時間只能有一種：

- 快速拔槍：近距離，手快但不準。
- 精準瞄準：遠距離，慢但準。

軍術士的身分是「用槍的施法者」，架勢讓這一點每場戰鬥都看得到。AI 依敵人距離自動切換。玩家這邊有兩顆便宜的按鈕，操作很直覺。

### 4.3 物品

| 物品 | 用途 | 取得 | 實作 |
|---|---|---|---|
| 軍術教範 Feldhandbuch | 學習法術、累積精煉值（3.3） | 艦隊商人、資深軍術士、任務 | `Book` + `BookOutcomeDoer_MagierManual` + `RulePackDef` |
| 專注安瓿 Konzentrat | 立即 +20% 心靈專注；之後 12 小時心靈敏感度 −10%，有輕微成癮 | 藥物實驗室製作（以中性胺為主，配方待定）；艦隊商人 | 原版 `IngestionOutcomeDoer_OffsetPsyfocus` + hediff |

專注安瓿的設定理由：前線沒時間冥想，所以艦隊用後勤解決施法者的補給問題。這和路徑描述的「結合現代科技與軍事訓練」一致。

### 4.4 延伸候選（不在第一批）

1. **附術彈 Zauberpatrone**（4 級）：「子彈比任何法術都快」，那就把術式灌進子彈。接下來 10 發命中時附加 EMP 5 或暈眩 1。這是最有代表性的點子，但需要對 `Bullet.Impact` 打 Harmony 修補，CE 的 `BulletCE` 還得另外修，所以先列為延伸。
2. **軍術士小隊**：1 名資深 + 2 名見習 + 1 隻石像鬼的襲擊組合。石像鬼在生成時就連結到資深軍術士，資深倒下時一起崩解。
3. **法師塔血脈普查**（任務）：艦隊派員檢測殖民地的心靈施法者。接受後，有心靈連結的殖民者各得一本教範與少量艦隊好感；拒絕則好感下降。
4. **術式訓練靶**（建築）：非戰鬥精煉值的固定來源，速率比照非戰鬥施放，上限一樣到 II。
5. 用 DMS 印表機複印教範（3.3 的待查證項目）。

## 5. 程式架構與檔案

### 5.1 結構調整

- `About.xml`：
  - 依賴改為 DMS Core + `ludeon.rimworld.royalty`，移除 VPE。
  - `loadAfter` 移除 VPE。
  - 1.6 怎麼處理見 D2。
- `LoadFolders.xml`：`v1.7` 改為 `/`、`1.7`、`1.7/CE`；內容從 `1.6/Defs` 移到 `1.7/Defs`。
- `.csproj`：只留 `net10.0`；移除 `VanillaPsycastsExpanded`、`VEF` 參考與 `VPEAssemblies`／`VEFAssemblies` 屬性；保留 FFF；加入 Harmony 參考。
- 刪除 `Ability_Flash.cs` 的 `RW_1_7` 反射段落。

### 5.2 C# 類別

| 現有 | 處理 |
|---|---|
| `Ability_Flash` | 改寫為 `CompAbilityEffect_Flash`，沿用扇形計算，預覽改用 `DrawEffectPreview` |
| `Ability_Repel` | 改寫為 `CompAbilityEffect_Repel`；表演用的爆炸改用原版 `GenExplosion` 直接呼叫 |
| `Ability_Swap` | 改寫為 `CompAbilityEffect_Swap` |
| `AbilityExtension_Construct`／`ConstructPawn`／`ConstructBase` | 改寫為 `CompAbilityEffect_Construct*`；連結改用 `CompMagierLink`／`CompDrone.SetPlatform` |
| `AbilityExtension_Heal`／`Calm`／`Destroy` | 改寫為對應的 `CompAbilityEffect_*`，邏輯不變 |
| `Hediff_Focus` | 併入 `Hediff_MagierDoctrine` |
| `Pawn_ConstructWeaponUsable` | 刪除（改用 `Fortified.WeaponUsableMech`） |
| `JobGiver_TaktikmagierCombat` | 改用原版 API，加上新法術判斷 |
| `MilitarmagierDefOf` | 移除 `VPE_PsychicEntropyMinimum`、`VFEA_UseAbility`；`MilitarmagierAbilityDefOf` 改以家族查找 |
| `CompAbilityEffect_RequireApparel`、`ConstructHeatExtension` | 保留 |

| 新增 | 用途 |
|---|---|
| `MagierSpellExtension` | 法術家族、階級、下一階 def、門檻 |
| `Hediff_MagierDoctrine` | 精煉紀錄、換階、構裝體佔用 |
| `CompAbilityEffect_Drill` | 施法時加精煉值 |
| `CompMagierLink` | 召喚者連結、解除 gizmo、召喚者倒地時崩解 |
| `CompAbilityEffect_EmpBurst` | 帶忽略清單的電磁脈衝（III 階敵我識別） |
| `CompAbilityEffect_Smoke`、`_FieldRepair`、`_Mark`、`_StanceExclusive` | 新法術 |
| `BookOutcomeDoer_MagierManual`（+ Properties） | 教範 |
| `MagierKindExtension` | 敵方軍術士產生 |

### 5.3 Harmony 修補

| 目標 | 類型 | 目的 |
|---|---|---|
| `Hediff_Psylink.TryGiveAbilityOfLevel` | prefix | 隨機發放時排除帶 `MagierSpellExtension` 的法術（D3） |
| `ThingDefGenerator_Neurotrainer.ImpliedThingDefs` | postfix | 不替軍術士法術產生心靈訓練器（否則每個法術會有 3 個） |
| `PawnGenerator.GeneratePawn` | postfix | 套用 `MagierKindExtension` |
| `Bullet.Impact`（延伸） | postfix | 附術彈 |

### 5.4 翻譯

- `DefInjected/VEF.Abilities.AbilityDef` → `DefInjected/AbilityDef`，只需要 I 階的條目。
- 刪除 `DefInjected/VanillaPsycastsExpanded.PsycasterPathDef`。路徑描述（阿尼瑪原星的段落）改為教範的描述與 `DMST_MagierDoctrine` 的說明，原文可以保留。
- 新增：精通特性（Keyed）、教範書名（RulePack）、新法術、新物品。

### 5.5 CE

- xpath 從 `VEF.Abilities.AbilityDef[defName=…]/range` 改為 `AbilityDef[@Name="DMST_xxxBase"]/verbProperties/range`。
- 半徑、成本的修補依新的 comp 結構重寫。
- 附術彈在 CE 下另外處理，或停用。

## 6. 貼圖需求清單

尺寸比照現有的法術圖示（128×128）。路徑是提案用的，可以改。

| 優先 | 路徑 | 尺寸 | 內容 |
|---|---|---|---|
| **必要**（第 0 階段） | `UI/Gizmos/DMST_BreakLink` | 128×128 | 「解除術式聯結」gizmo，取代 VPE 的 `BreakLinkSteelConstruct`／`BreakLinkRockConstruct` |
| 必要（第 1 階段） | `Things/Item/Book/DMST_FieldManual/FieldManual` | 與原版教科書相同 | 教範：地上與圖示 |
| 必要（第 1 階段） | `…/FieldManual_Open_north`／`_east`／`_south` | 同上 | 閱讀中攤開的樣子（Graphic_Multi） |
| 必要（第 1 階段） | `…/FieldManual_Vertical_north`／`_east`／`_south` | 同上 | 放在書櫃裡（Graphic_Multi） |
| 必要（第 2 階段） | `Abilities/Militarmagier/mark`、`steadyAim`、`smoke`、`fieldRepair`、`ward` | 128×128 | 5 個新法術圖示 |
| 必要（第 2 階段） | `Things/Item/Drug/DMST_Konzentrat` | 64×64 | 專注安瓿 |
| 可選 | `UI/Overlays/DMST_Tier_II`、`_III` | 32×32 | gizmo 角落的階級標記（沒有就只在名稱後面顯示 II／III） |
| 可選 | `Things/Mote/DMST_MarkOverhead` | 64×64 | 被標定目標頭上的記號 |
| 延伸 | `Abilities/Militarmagier/spellRounds` | 128×128 | 附術彈 |
| 延伸 | `Things/Building/DMST_TrainingTarget` | 依建築尺寸 | 術式訓練靶 |

不需要新圖的部分：石像鬼、哨戒砲塔、頭盔、斗篷、背景圖都沿用現有素材；殘骸、血跡、煙霧用原版的。

## 7. 分階段實作與驗收

| 階段 | 內容 | 驗收 |
|---|---|---|
| **0 等價移植** | 5.1 結構調整；10 個法術與構裝體改用原版；AI；敵方兵種；翻譯；CE | 只開 Harmony、Core、Royalty、Biotech、FFF、DMS 時零錯誤；三階敵方軍術士正常生成並施法；石像鬼只能跟著召喚者徵召；構裝體佔用正確，召喚者倒地時崩解；有 CE 時修補無錯誤 |
| 1 教範與精煉 | 3.3–3.5；I／II／III 三階 def；`Hediff_MagierDoctrine` | 讀完教範會學會法術；換階時冷卻沿用；精煉值存讀檔正確；軍術士法術不會出現在隨機升級或心靈訓練器中 |
| 2 新法術與物品 | 4.1–4.3 | 架勢互斥；標定可以讓隱形的敵人現形；電磁脈衝 III 不打到己方機械體 |
| 3 延伸 | 4.4 依你的選擇 | 個別訂定 |

第 0 階段完成時就可以發布 1.7 版：功能與現在相同，但不再依賴 VPE／VEF。之後的階段可以分批更新。

## 8. 決策點（待定）

| # | 問題 | 建議 | 替代方案 |
|---|---|---|---|
| D1 | 皇權改為硬依賴？ | **是**。原版心靈技能沒有皇權無法運作（2.3），而 VPE 本來就要求皇權 | 自己做一套不靠神經熱的資源：工程量大，也會失去冥想、心靈敏感度等原版綜效 |
| D2 | 1.6 怎麼處理？ | ~~1.6 凍結在現有的 VPE 版~~。**更正（2026-10-17）**：DMS 2.0.3 的 `LoadFolders.xml` 只有 `v1.7` 節點，在 1.6 下本體什麼都不載入，所以凍結的 1.6 版會因為缺少本體 Def 而無法運作。初稿推薦這個選項是推理錯誤。改為建議**完全放棄 1.6**，與 DMS 2.0.3 一致 | 凍結 1.6（只對手上還留著舊版 DMS 本體的玩家有用） |
| D3 | 軍術士法術要不要排除在隨機升級與心靈訓練器之外？ | **兩者都排除**，教範是唯一的取得途徑 | 只排除心靈訓練器，讓一般施法者偶爾「無師自通」 |
| D4 | 精通（III）只能靠實戰嗎？ | **是**，教範與訓練只到 II | 傳說品質的教範也能讀到 III |
| D5 | 電磁脈衝 I、II 階無差別攻擊？ | **是**，敵我識別作為精通特性 | 一開始就不打友軍，另找別的精通特性 |
| D6 | 存檔相容？ | **不相容**。VPE 的法術資料無法轉移；與 DMS 2 一起開新檔 | 提供除錯動作「依目前心靈連結等級重新發放軍術士法術」作為補救 |
| D7 | 石像鬼改為機械體（FFF 無人機）？ | **是**：可以拿掉 VEF 的 comp，DMS 也有現成範例 | 繼續當動物型構裝體：必須自己寫徵召與免疫 |
| D8 | 第一批新法術的範圍？ | 4.1 的 5 個全做 | 先做標定與戰地重構，其他晚點 |

## 9. 風險

- **熱值平衡**：VPE 與原版的熱上限尺度不同，3.2 的表只是起點，需要用 1、3、5 級施法者各實測一場戰鬥。
- **換階時機**：同一法術已經排入施法佇列時換 def，佇列的 job 會指向被移除的 ability。換階要等 `CurJob` 與佇列中都沒有該法術時才執行（3.4 的延後換階就是為了這個）。
- **AI 開銷**：`JobGiver_TaktikmagierCombat` 掛在 `HumanlikeConstant`，新法術的判斷要維持「先做便宜的排除」的寫法。
- **CE**：法術射程、構裝體護甲要重新對照；附術彈需要另外修補 CE。
- **石像鬼變成機械體**以後會受機械體相關機制影響（EMP、機械師的指令選單、殺死機械體的心情）。要確認 `CompDrone` 在沒有平台時的行為（`CanDraft` 會回報「沒有控制平台」），所以召喚當下就必須 `SetPlatform`。

## 10. 第 0 階段實作紀錄（2026-10-18）

狀態：**已實作並編譯通過（0 警告、0 錯誤），尚未在遊戲中驗證**。第 7 節第 0 階段的驗收項目全部待跑。

已做的靜態檢查：35 個 XML 檔都能解析；XML 引用的 18 個 `Militarmagier.*` 類別都存在於原始碼；引用的外部 Def（`PsycastBase`、`DMS_BaseDroidRace`、`FFF_DroneMain` 等）都存在，而且 FFF 在 1.7 會載入它的 `1.6/` 資料夾。

結構：

- 1.7 單一版本（D2 改為放棄 1.6）：`1.6/Defs`、`1.6/CE` 用 `git mv` 移到 `1.7/`，刪除 `1.6/Assemblies` 與 VPE 路徑定義。
- About.xml 依賴改為 Harmony、Royalty、DMS Core；LoadFolders 只剩 v1.7。
- `.csproj` 只建置 `net10.0`，移除 VPE／VEF，加入 `Lib.Harmony` 3.0.0-rc.2（與 FFF 相同）。

與提案不同之處：

1. **D3 的排除（隨機升級、心靈訓練器）延到第 1 階段**。教範要第 1 階段才有，現在排除的話玩家完全拿不到軍術士法術。第 7 節本來就把這項驗收放在第 1 階段。第 0 階段的取得途徑暫時是原版的心靈訓練器與升級隨機獎勵。
2. **構裝體佔用改用 `StatPart_SustainedConstructs`**（以 `1.7/Patches/Stat_PsychicEntropyMax.xml` 加到 `PsychicEntropyMax`），不用 hediff 的 statOffset。心靈連結的熱上限係數是乘算，offset 會被放大（4 級約 ×2.4）；StatPart 在乘算之後才扣，扣的就是 Def 寫的點數，屬性明細也會列出「維持中的構裝體 −N」。
3. `Hediff_Focus` 先保留為獨立的維持 hediff，同時追蹤砲塔與石像鬼。與精煉紀錄合併（3.6 的 `DMST_MagierDoctrine`）留到第 1 階段。
4. `CompMagierLink` 的崩解條件：召喚者死亡、倒地，或不在同一張地圖。手動解除時，砲塔用 `Vanish` 消失而不是被擊毀，避免「召喚後立即解除」把零件換成鋼鐵。
5. 石像鬼的 comps 只保留 FFF 無人機與 `CompMagierLink`，刻意不繼承 DMS 無人機的休眠、回收、機械維修。
6. AI（`JobGiver_TaktikmagierCombat`）不能用 `Ability.GizmoDisabled` 判斷（它會擋下所有未徵召單位）；而 `Psycast.CanCast` 在有熱消耗時不檢查專注。所以專注與專注區間改為手動檢查。
7. 敵方兵種：`MagierKindExtension` 沿用 VPE 版的等級與數量（見習 2 級 1～3 個、戰術 4 級 2～10 個、資深 6 級 6～14 個）。生成時暫時抑制原版「每升一級送一個隨機心靈技能」。
8. 戰地急救瞄準心靈敏感度為 0 的目標時直接拒絕（新 Keyed `DMST_TargetPsychicallyDeaf`），不讓原版先扣熱再顯示「免疫」。
9. 移除頭盔與斗篷配方中的 `VPE_Eltex`，**沒有換成其他材料**（2.4 的建議未做）；斗篷 +0.75 熱上限也未調整。
10. CE：法術修補改用原版 xpath；修正石像鬼 `tools` 取代後缺少 `Inherit="False"`，否則會從 DMS 無人機基底多繼承一個頭槌。電磁脈衝的 CE 熱值沿用 30（非 CE 現在是 20），已在修補內註記待平衡。
11. 翻譯：`DefInjected/VEF.Abilities.AbilityDef` 改名為 `AbilityDef`；構裝體相關的兩段說明（從「抬高最低心靈熵」改為「降低心靈熵上限」）已同步更新簡繁中文；新增 5 條 Keyed（英、繁、簡）。
12. 解除聯結的 gizmo 圖示暫時用原版的取消圖示（`UI/Designators/Cancel`）。貼圖放到 `UI/Gizmos/DMST_BreakLink` 就會自動改用。

待驗證（遊戲內）：

- 自身施法法術（排斥、快速拔槍、電磁脈衝、強制清醒）能否正常施放；突擊閃光的扇形預覽與命中。
- 石像鬼：徵召只跟著召喚者、未徵召時的行為（FFF 無人機思考樹：待在召喚者附近，持有武器時交戰）、死亡變成鋼渣並掉落武器。
- 構裝體佔用的熱上限數值與屬性明細；容量不足時召喚按鈕會被停用。
- 敵方三階軍術士的心靈連結等級與法術組合，AI 施法。
- 召喚者帶著石像鬼組成商隊時：石像鬼離開地圖會從佔用中移除，回到地圖後不會重新登記（已知的小缺口）。
