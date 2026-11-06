# NoiseFish（大鱼吃小鱼）— 项目长期记忆

> 工程：`C:\Users\1\Desktop\wdyouxi\zhanchang\NoiseFish`
> Unity **2022.3.62f3c1** · **Y-up**（与 zhizhu 的 Z-up 不同，别混）
> git：已初始化，main 分支，每步一个 commit

---

## 一、核心准则（用户定案，不可动摇）

1. **注视为关键** —— 一切机制挂在「注视」上
2. **注视会显性** —— 被注视的鱼显形
3. **不注视会消散** —— 失去注视的鱼消散
4. **部分鱼累积注视时间后会消散** —— 有些鱼看够时长就自己散掉
5. **消散后随机出生点出生** —— 下一条鱼在随机出生点出现
6. **不搞动态变大** —— 玩家操控的鱼、其他鱼都是**同样大小**（不做尺寸成长）
7. **不用 MC 地图当鱼缸** —— 地图重新规划

---

## 二、四步计划（用户定）

| 步骤 | 内容 | 状态 |
|---|---|---|
| 1 | 空房间跑通「注视显性 / 不注视消散」 | ✅ 代码完成，待Unity 内实测 |
| 2 | 鱼加多组成部分 → 分段消散 | ⬅ 下一步 |
| 3 | 画地图 | 待做 |
| 4 | 再考虑别的 | 待定 |

**第 2 步的关键设计问题（提前想，别到时候返工）**：
- 分段消散顺序：尾→头（被吞噬感）还是随机？
- 每段是独立 GameObject 还是同一 mesh 分块？
- 命中哪一段有效果？
- **建议第 1 步的鱼就按「多段可拼」结构搭**（现在还是单球体灰盒，第 2 步要重构）

---

## 三、当前代码结构

```
Assets/Scripts/
  FishSpawner.cs    ★ 注视核心（240 行）—— 显形/消散/随机出生点全在这
  FishActor.cs      鱼本体（148 行）—— 漂移 + 消散 + 显形 SetPresence + 高亮 SetHighlighted
  PlayerSwim.cs     第一人称相机移动（相机即玩家，Y 锁演出平面）
Assets/Editor/
  DemoSceneBuilder.cs  一键生成场景（空房间 + 6 出生点 + 鱼 prefab）
  MCImporter.cs        ⚠️ MC 地图导入，本项目不用，待清理
```

**Unity 菜单**：`Tools/生成大鱼吃小鱼演示场景` → 生成 `Assets/Scenes/EatDemo.unity`
**运行**：▶ 播放 → WASD 移动，左键按住朝视线深处前进

### 关键参数（FishSpawner Inspector）
| 参数 | 默认 | 含义 |
|---|---|---|
| viewAngle | 35° | 视锥半角 |
| viewDistance | 25 | 注视距离上限 |
| appearTime | 1s | 注视多久显形 |
| graceTime | 0.5s | 脱离注视后宽限 |
| fishScale | 0.5 | 鱼固定尺寸 |
| selfDissolveChance | 0.35 | 「看够就散」型鱼的概率 |
| selfDissolveDuration | 6s | 该型鱼的注视时长上限 |

---

## 四、工具链

- **编译检查**：`python .workbuddy/tools/cscheck.py`
  - 沙箱无 Unity，这是唯一的编译前关卡
  - 检查：花括号深度平衡、类型声明配对、Unity API 误用、缺分号、跨脚本 private 访问
  - `--baseline` 与行数基线对比（防误删，行数差 >40 报警）
  - `--update-baseline` 更新基线
  - ⚠️ **已用注入错误验证过**能真实报错，不是永远通过的空壳
- **场景生成**：`DemoSceneBuilder.Build()`（Editor 菜单），需在 Unity 内执行

---

## 五、git 纪律

- 仓库身份：`WorkBuddy` / `workbuddy@local`（仓库级；**全局 user.name/email 未配置**）
- **没有远端**。用户原话规矩是「每变更单独提交 + 推送远程」，但 NoiseFish 尚未建远端
- `.gitignore` 已排除 `Library/ Temp/ obj/ Logs/ UserSettings/ *.csproj` 等生成物
- 提交历史：
  - `ce7aa87` 建立 git 基线（接手时的原始骨架）
  - `02a1421` 第 1 步核心逻辑

---

## 六、待清理 / 待确认

- `Assets/Editor/MCImporter.cs` + `Assets/MCImport/`（59万 dirt 等）—— **本项目不用**，用户已确认不拿 MC 地图当鱼缸，但**尚未删除**（等用户点头）
- `Assets/Resources/BillingMode.json`（`{"androidStore":"GooglePlay"}`）—— 移动端发布配置，暂时无用
- 远端仓库是否要建（用户规矩要求推送远程）
- 全局 git 身份未配置，影响其他仓库

---

## 七、相关项目（不要搞混）

| 项目 | 路径 | 说明 |
|---|---|---|
| **NoiseFish**（本项目） | `C:\Users\1\Desktop\wdyouxi\zhanchang\NoiseFish` | 大鱼吃小鱼，注视制 |
| zhizhu | `C:\Users\1\Desktop\wdyouxi\zhanchang\zhizhu` | 体素蜘蛛丝，**Z-up**，8 键硬规则 |
| 战争进化史 | `C:\Users\1\Desktop\wdyouxi\zhanchang\warfare-evolution` | 仅文档（585 行规格），Unity 工程未建 |
| In the Balance | `D:\qimayukansha\inthebalance\youxibenti\In the Balance` | 战棋+肉鸽卡牌，**Plastic 版本控制**，单文件 BattleGrid3D.cs |
