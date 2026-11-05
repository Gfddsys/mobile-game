# Skins, Leaderboard & Monetization

สรุประบบที่ทำไว้แล้ว และสิ่งที่ต้องทำต่อเพื่อให้ใช้ **เงินจริง / โฆษณาจริง / Leaderboard ออนไลน์** ได้

> ตอนนี้ทุกอย่างทำงานครบในโหมดทดสอบ (โฆษณาปลอม, ร้านค้าปลอม, ตารางคะแนนในเครื่อง)
> **โหมดทดสอบทำงานเฉพาะใน Editor และ Development Build** — ใน release build ที่ยังไม่ได้ต่อ SDK จริง
> ปุ่มดูโฆษณาจะไม่ขึ้น และร้านค้าจะขึ้นว่า "Store is not configured" (กันไม่ให้แจกของฟรีโดยไม่ตั้งใจ)

## สิ่งที่มีแล้ว

| ระบบ | ไฟล์ | หมายเหตุ |
|---|---|---|
| 7 สกิน (ฟรี 1 + ซื้อด้วยเหรียญ 5 + เงินจริง 1) | `Upgrades/SkinDatabase.cs`, `Art/SpriteLibrary.cs` (`GetTurtle`) | สลับสีด้วยโค้ด + อุปกรณ์ (แว่น/มงกุฎ/ผ้าคาด), Rainbow Hero เปลี่ยนสีวนไปเรื่อยๆ |
| หน้า Skins / Shop / Ranks | `UI/UIRoot.Monetization.cs` | เมนูหลัก: ปุ่ม SKINS, SHOP, RANKS (คีย์ K / B / L) |
| ดูโฆษณา x2 เหรียญ | `GameManager.WatchAdForDoubleCoins` | หน้าสรุป, ใช้ได้ 1 ครั้ง/รอบ (คีย์ A) |
| ดูโฆษณาชุบชีวิต | `GameManager.DyingRoutine` / `Revive` | 1 ครั้ง/รอบ, ต้องวิ่งเกิน 30 m, เกิดใหม่ถอยหลัง 3 m + ล้างสิ่งกีดขวาง 25 m + อมตะ 3 วิ |
| ขายสกินเงินจริง | `ProductCatalog.SkinRainbow` (`skin_rainbow`) | non-consumable |
| ขายแพคเหรียญ | `coins_small` 1,000 / `coins_medium` 5,500 / `coins_large` 12,000 | consumable |
| Leaderboard ระยะทาง | `LocalLeaderboard` | Top 20 ในเครื่อง, ส่งผลทุกครั้งที่จบรอบ, แสดงอันดับในหน้าสรุป |

ค่าที่ปรับได้: `GameConfig` หมวด **Monetization** (`reviveMinDistance`, `reviveOfferSeconds`, `reviveShieldSeconds`, `reviveClearDistance`)
ราคาสกินเหรียญ: `SkinDatabase.cs` — ราคาเงินจริงและจำนวนเหรียญต่อแพค: `ProductCatalog` ใน `Monetization/Services.cs`

## สถาปัตยกรรม

เกมคุยกับ 3 interface เท่านั้น (`Monetization/Services.cs`):

```
Services.Ads          : IAdService           -> MockAdService       (แทนด้วย LevelPlay / Unity Ads)
Services.Purchases    : IPurchaseService     -> MockPurchaseService (แทนด้วย Unity IAP)
Services.Leaderboard  : ILeaderboardService  -> LocalLeaderboard    (แทนด้วย UGS Leaderboards)
```

การส่งของที่ซื้อทำที่ `Store.Grant` ที่เดียว — เรียก **หลังจาก store ยืนยันการจ่ายเงินแล้วเท่านั้น**

ต่อ SDK จริง = เขียนคลาสใหม่ที่ implement interface แล้วตั้งค่าใน `GameManager.Awake` เช่น
```csharp
Services.Ads = new LevelPlayAdService();
Services.Purchases = new UnityIapService();
Services.Leaderboard = new UgsLeaderboardService();
```

## ขั้นตอนต่อของจริง (สำหรับคนพัฒนาต่อ)

### 1. โฆษณา (Rewarded) — Unity LevelPlay
1. สร้างบัญชี Unity LevelPlay / ironSource, สร้างแอป Android (+iOS), ได้ App Key และ Rewarded Ad Unit ID
2. ติดตั้งแพ็กเกจ Ads Mediation (LevelPlay) ผ่าน Package Manager
3. เขียน `LevelPlayAdService : IAdService`
   - `IsRewardedReady` = ad โหลดเสร็จแล้ว
   - `ShowRewarded` → เรียก `onComplete(true)` **เฉพาะ** ตอนได้ callback rewarded (ดูจบ), ปิดก่อน = `false`
4. ทำ consent: GDPR/UMP (ยุโรป), ATT (iOS), ตั้ง COPPA ให้ถูกกับกลุ่มอายุผู้เล่น
5. ใส่ privacy policy ใน store listing

### 2. ซื้อด้วยเงินจริง — Unity IAP
1. สร้างสินค้าใน Google Play Console / App Store Connect ด้วย product id ตรงกับ `ProductCatalog`:
   `coins_small`, `coins_medium`, `coins_large` (consumable), `skin_rainbow` (non-consumable)
2. ติดตั้ง `com.unity.purchasing`
3. เขียน `UnityIapService : IPurchaseService`
   - `GetPriceString` ใช้ราคาที่ store ส่งมา (localized) — ห้าม hard-code ราคา
   - `Purchase` → `onResult(true)` หลัง purchase ถูก confirm แล้วเท่านั้น
   - `RestorePurchases` คืน id ของ non-consumable (`skin_rainbow`) — **iOS บังคับให้มีปุ่ม Restore** (มีแล้วในหน้า Shop)
4. แนะนำให้ตรวจ receipt ฝั่งเซิร์ฟเวอร์ก่อน grant (กันโกง) — ตอนนี้ยังไม่มีเซิร์ฟเวอร์
5. เงินในเกมเก็บใน PlayerPrefs (แก้ไขได้บนเครื่องที่ root/jailbreak) — ถ้าจริงจังเรื่อง economy ให้ย้ายไป Cloud Save / Economy ของ UGS

### 3. Leaderboard ออนไลน์ — Unity Gaming Services
1. ลิงก์โปรเจกต์กับ Unity Cloud, เปิด Authentication + Leaderboards, สร้าง leaderboard id เช่น `best_distance` (sort: descending, update: keep best)
2. ติดตั้ง `com.unity.services.authentication` และ `com.unity.services.leaderboards`
3. เขียน `UgsLeaderboardService : ILeaderboardService`
   - เริ่ม: `UnityServices.InitializeAsync()` + sign in anonymously
   - `Submit` → `AddPlayerScoreAsync("best_distance", distance)` แล้วเรียก `onRank(rank+1)` เมื่อเซิร์ฟเวอร์ตอบ
   - `GetTop` → `GetScoresAsync(..., limit = count)`
4. ชื่อผู้เล่นตอนนี้สุ่มเป็น `Turtle####` (`SaveData.playerName`) — ถ้าจะให้ตั้งชื่อเอง ต้องทำหน้ากรอกชื่อ + กรองคำหยาบ

## การทดสอบ
- `Tests/MonetizationTests.cs` ทดสอบ: ซื้อ/ใส่สกิน, สกินเงินจริงได้เฉพาะเมื่อจ่ายสำเร็จ, แพคเหรียญ, leaderboard เรียง/จำกัด 20, ชุบชีวิต (รับ/ปฏิเสธ/1 ครั้งต่อรอบ), x2 เหรียญ (ดูจบ/ข้าม)
- ในเทสต์ใช้ `MockAdService.AutoResult` / `MockPurchaseService.AutoResult` เพื่อจำลองผลโดยไม่ต้องกดปุ่ม
- ใน Editor: กดปุ่มจริงจะเห็นหน้าต่าง "TEST AD" (รอ 3 วิ แล้วกด CLAIM) และ "TEST PURCHASE" (ไม่เสียเงิน)

## ใส่ภาพสกินที่วาดเอง
วาง PNG ชื่อ `turtle_{id}.png` ใน `Assets/_Project/Resources/Sprites/` (สกินเริ่มต้นใช้ `turtle.png`)
id: `blue, sunset, bubblegum, golden, ninja, rainbow` — ขนาด 26×20 px, PPU 16, Filter Point
