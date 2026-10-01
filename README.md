# 🐢 Turtle Blaster (เต่าขี่สเก็ตบอร์ด)

เกมมือถือ Unity 2D แนว **Physics-based Endless Runner / Extreme Sports Arcade** ตาม
*Game Design Document – TURTLE BLASTER* — เต่าธรรมดาที่ประดิษฐ์สเก็ตบอร์ดติดไอพ่นจากของใช้รอบตัว
(ถังดับเพลิง, กระป๋องสเปรย์, ท่อบั้งไฟ) แล้วไถลลงเนินด้วยฟิสิกส์สมจริง

> Unity **6000.3.2f1** · Built-in Render Pipeline (2D) · Legacy Input Manager · เป้าหมาย: Android (landscape), iOS ได้ในอนาคต

---

## เริ่มต้นใช้งาน (สำหรับ dev ใหม่)

1. ติดตั้ง Unity **6000.3.2f1** (พร้อม Android Build Support ถ้าจะ build มือถือ)
2. เปิดโฟลเดอร์นี้ด้วย Unity Hub (Add project from disk)
3. เปิดซีน `Assets/_Project/Scenes/Game.unity` แล้วกด **Play**
   - ถ้าซีน/ค่าตั้งต้นหาย: เมนู **Turtle Blaster ▸ Setup ▸ Run All** (รันซ้ำได้ปลอดภัย)
4. ควบคุมใน Editor ด้วยคีย์บอร์ด (หรือเมาส์คลิกปุ่มบนจอ):

| มือ | ปุ่ม | ฟังก์ชัน |
|---|---|---|
| ซ้าย (แกนตั้ง) | ↑ / W | Pitch Up – เชิดหน้า + เปิดไอพ่น (กินเชื้อเพลิง) |
| ซ้าย (แกนตั้ง) | ↓ / S | Pitch Down – กดหน้า / **Ground Slam** กลางอากาศ / **หมอบ** หลบของสูง |
| ขวา (แกนนอน) | ← / A | Backflip (หมุนทวนเข็ม) |
| ขวา (แกนนอน) | → / D | Frontflip (หมุนตามเข็ม) |
| | Space | **Jump** (กระโดดข้ามกรวย ไม่เสียเชื้อเพลิง) |
| | Esc / P | Pause |

บนมือถือจะมีปุ่มสัมผัส 4 ปุ่ม (ซ้าย = ↑↓, ขวา = flip หลัง/หน้า) รองรับ multi-touch

### Build Android
> ⚠️ **Unity ไม่รองรับ path ที่มีอักษรที่ไม่ใช่ ASCII (เช่นชื่อโฟลเดอร์ภาษาไทย) สำหรับ Android build** — เล่น/แก้ใน Editor ได้ปกติ
> แต่ก่อน build ให้ย้าย/คัดลอกโปรเจกต์ไปไว้ที่ path ภาษาอังกฤษ เช่น `C:\Projects\TurtleBlaster` (คัดลอกแค่ `Assets`, `Packages`, `ProjectSettings`)

เมนู **Turtle Blaster ▸ Build ▸ Android APK** → ได้ไฟล์ `Builds/Android/TurtleBlaster.apk`
(หรือใช้ File ▸ Build Profiles ตามปกติ — Package: `com.turtleblaster.game`, min API 25, IL2CPP ARM64+ARMv7)

### รันเทสต์
Window ▸ General ▸ Test Runner ▸ PlayMode  หรือ command line:
```bash
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults results.xml
```
เทสต์ครอบคลุม: ระบบอัปเกรด/เซฟ, ภูมิประเทศ deterministic, การตัดสิน Perfect/Rough/Fatal Landing,
นับตีลังกา, จบเกม→บันทึกรางวัล, และบอทจำลองการเล่น (ตรวจว่าฟิสิกส์ไม่ฆ่าผู้เล่นเอง)

---

## ภาพรวมสถาปัตยกรรม

เกมสร้าง **ทุกอย่างด้วยโค้ด** (กล้อง, โลก, ตัวละคร, UI, เสียง, สไปรต์ placeholder) เพื่อให้ซีนมีแค่ `GameManager` ตัวเดียว
ทำให้ dev คนต่อไปอ่านโค้ดทีเดียวเข้าใจ และค่อยๆ ย้ายไปเป็น Prefab/Asset ทีหลังได้โดยไม่กระทบ logic

```
Assets/_Project/
├── Scenes/Game.unity            ซีนเดียว (Camera + GameManager)
├── Resources/
│   ├── GameConfig.asset         ★ ค่า balance ทั้งหมด (ปรับได้โดยไม่แตะโค้ด)
│   ├── Sprites/                 ← ใส่ PNG ทับของ placeholder ที่นี่ (ชื่อไฟล์ต้องตรงกัน)
│   └── Audio/                   ← ใส่เสียง/เพลงทับของ placeholder ที่นี่
├── Art/Placeholders/            สไปรต์ placeholder ที่ export ออกมาเป็น PNG (ใช้เป็นต้นแบบให้ศิลปิน)
└── Scripts/
    ├── Runtime/ (asmdef: TurtleBlaster.Runtime)
    │   ├── Core/     GameManager, GameConfig, GameInput, SaveData(+RunStats)
    │   ├── Player/   TurtleController   ← ฟิสิกส์ + กติกาหลักทั้งหมด
    │   ├── World/    WorldGenerator, CameraFollow, ParallaxBackground, WorldTypes
    │   ├── Upgrades/ UpgradeDatabase    ← ตารางอัปเกรด Garage
    │   ├── UI/       UIRoot (HUD/Menu/Garage/Summary/Pause), UIFactory
    │   ├── Audio/    AudioManager       ← สังเคราะห์ chiptune/SFX ด้วยโค้ด
    │   └── Art/      PixelCanvas, SpriteLibrary ← วาด pixel-art placeholder ด้วยโค้ด
    ├── Editor/  ProjectSetup (เมนู Turtle Blaster)
    └── Tests/   PlayMode tests
```

### Game flow (GDD ข้อ 2)
`Menu (attract mode: เต่าไถอัตโนมัติเป็นฉากหลัง)` → `StartRun` → `Playing` → ชน/ตกพื้น → `Dying` (slow-mo + **ถ่าย Snapshot ท่าล้ม**)
→ `Summary` (สถิติ + รูป polaroid) → `Garage` / Retry

`GameManager` รับ **event** จาก `TurtleController` (Landed, FlipInAir, FlipsCommitted, PickedUp, HazardHandled,
SuperBoostChanged, Grinding, Crashed) แล้วจัดการคะแนน/คอมโบ/ป๊อปอัป/เสียง — controller ไม่รู้จัก UI เลย

### สิ่งที่ implement ตาม GDD

| GDD | ที่อยู่ในโค้ด |
|---|---|
| 3.1 ควบคุม 2 แกน | `GameInput` (คีย์บอร์ด + ปุ่มสัมผัส), `UIRoot.BuildTouchControls` |
| 3.2 Auto-cruise, แรงโน้มถ่วง, แรงเฉื่อยการหมุน | `TurtleController.FixedUpdate` (ใน Air ไม่มี assist = แรงเฉื่อยล้วน; บนพื้นบอร์ดปรับระนาบตามความลาดอัตโนมัติ) |
| 3.3 Perfect ≤12° (บนทางลาดลง) +25% speed +5% fuel / Rough 13–35° −20% / Fatal >35° หรือกระดองกระแทก → Ragdoll | `TurtleController.EvaluateLanding`, `Crash` |
| 3.4 Stunt meter → Super Boost (ชนทะลุของเล็ก) | `TurtleController` (`Stunt01`, `StartSuperBoost`) |
| 4 ด่าน: เนิน, กรวย, หลุม, หินกลิ้ง, **ราว Grind**, ป้ายต่ำ, สายเคเบิล, นก | `WorldGenerator` (สุ่มแบบ deterministic ตาม seed; ความยากเพิ่มตามระยะทาง) |
| 5 Garage: Jet Thruster / Fuel Tank / Wheels / Safety Shell (ลำดับตาม GDD) | `UpgradeDatabase`, `UIRoot.BuildGarage`, `SaveData` |
| 6 Pixel art 16-bit, ควัน/ประกายไฟ, ragdoll ฮาๆ, chiptune | `SpriteLibrary`, particles ใน `TurtleController`, `AudioManager` |

> **การตีความที่ผมเลือกเอง** (GDD ไม่ได้ระบุ — แก้ได้ใน `GameConfig`/โค้ด):
> - ไม่มีปุ่มกระโดด: แรงส่งขึ้นอากาศมาจากเนิน และการเชิดหน้า + ไอพ่น — ไอพ่นดันทั้งไปข้างหน้าและยกตัวขึ้น (`thrustLift`) จึงกระโดดข้ามกรวยบนพื้นราบได้
> - สิ่งกีดขวางเบาที่ชน **โดยไม่มี Safety Shell = ล้มทันที**; Knee Pads รับ 1 ครั้ง/รอบ, Reinforced Shell รับ 2 ครั้ง/รอบ (เสียความเร็วตามระดับ)
> - ของที่ลอยสูง (ป้าย/สายเคเบิล/นก) หลบได้ด้วยการ **หมอบ (กด ↓)**
> - Super Boost เปิดอัตโนมัติเมื่อมิเตอร์เต็ม นาน 5 วินาที
> - Perfect Landing ต้องลงบน "ทางลาดลง" ตาม GDD; ลงราบมุมดี = "Clean" (ไม่มีโบนัส ไม่มีโทษ)
> - หยุดนิ่งเกิน 3.5 วินาที (ติดเนิน) = จบเกม (Stalled out)

---

## ทำงานต่อได้ยังไง

### ปรับ balance
แก้ `Resources/GameConfig.asset` (Inspector) — ความเร็ว, แรงไอพ่น, มุม Landing, ความถี่สิ่งกีดขวาง, คะแนน ฯลฯ  
ตารางอัปเกรด/ราคา: `Upgrades/UpgradeDatabase.cs`

### เปลี่ยนงานภาพ
ตอนนี้สไปรต์ทั้งหมดเป็น placeholder ที่วาดด้วยโค้ด (`Art/SpriteLibrary.cs`)
1. เมนู **Turtle Blaster ▸ Art ▸ Export Placeholder Sprites to PNG** → ได้ PNG ที่ `Art/Placeholders/` (ขนาดและ PPU ถูกต้อง)
2. ศิลปินวาดทับ แล้ววางไฟล์ **ชื่อเดียวกัน** ลง `Resources/Sprites/` → เกมใช้ไฟล์นั้นแทนอัตโนมัติ (ไม่ต้องแก้โค้ด)
   ชื่อที่ใช้: `turtle, board, wheel, thruster_0..2, cone, pothole, sign, cable, boulder, rail, bird_0, bird_1, coin, part, cloud, hills_far, hills_near, sky, ui_*`
3. สไปรต์ตั้ง Filter = Point, Compression = None, PPU = 16 (UI = 100)

เสียงเหมือนกัน: วาง AudioClip ชื่อ `coin, part, perfect, rough, flip, boost, hit, crash, click, buy, deny, thrust, music_run` ใน `Resources/Audio/`

### ย้ายไป New Input System / จอยเกมแพด
แก้ที่ `Core/GameInput.cs` ไฟล์เดียว (ที่อื่นอ่านแค่ `GameInput.Up/Down/Left/Right`)

### แปลภาษา
ข้อความ UI เป็นภาษาอังกฤษ (ฟอนต์ built-in ไม่มีอักษรไทย) — ถ้าจะใส่ไทย: เปลี่ยน `UIFactory.DefaultFont` เป็นฟอนต์ไทย หรือย้ายไป TextMeshPro + Unity Localization

### ไอเดียต่อยอด (ยังไม่ได้ทำ)
- ธีมด่านเพิ่ม (กลางคืน/ฝน), เควสต์รายวัน, Leaderboard (Unity Gaming Services), โฆษณา Rewarded (ดับเบิลเหรียญ), IAP
- แชร์รูป Snapshot (ตอนนี้เซฟไว้ที่ `Application.persistentDataPath/wipeouts/`)
- Ragdoll หลายชิ้นส่วน (ตอนนี้เป็นเต่าก้อนเดียวหมุนกระเด้ง), อนิเมชันตาเหลือก
- ปรับ hazard spawner เป็น pattern/ScriptableObject ให้ designer ออกแบบด่านเองได้

## ข้อควรรู้ / ข้อจำกัดที่ทราบ
- บิลด์ Android: ผมลองแล้ว สคริปต์ C# + IL2CPP คอมไพล์ผ่าน แต่ขั้น Gradle ล้มเพราะข้อจำกัดของเครื่อง/สภาพแวดล้อมที่ผมรัน (`Unable to establish loopback connection`) จึง **ยังไม่มีไฟล์ APK ที่ยืนยันแล้ว** — ลอง build บนเครื่องคุณที่ path ภาษาอังกฤษ
- เล่นบนมือถือจริง **ยังไม่ได้ทดสอบ** (ทดสอบผ่าน Editor + PlayMode tests แบบ headless เท่านั้น) — ค่า balance น่าจะต้องจูนเมื่อลองเล่นด้วยนิ้วจริง
- Physics layer 8 ใช้เป็น "Rail" (ไม่ได้ตั้งชื่อใน TagManager — ตั้งชื่อเองได้ ไม่กระทบโค้ด)
