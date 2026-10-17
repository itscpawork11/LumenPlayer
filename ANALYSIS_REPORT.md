# تقرير تحليل و خطة مراحل: مشغل Windows Native بديل لواجهة VLC

## 1. ملخص تنفيذي

- بيئة العمل الحالية (`E:\App`) فارغة: لا يوجد مشروع، لا git، ولا حزم NuGet مثبتة.
- VLC مثبت على النظام (الإصدار `3.0.24`, 64-bit, `C:\Program Files\VideoLAN\VLC\vlc.exe`).
- تتوفر .NET Framework 4.8 (Client/Full)، لكن لا يوجد .NET SDK 현代 (dotnet.exe) في PATH. تتوفر `csc.exe` لـ .NET Framework.
- REA قادرة على فتح VLC كملف PE وجمع أدلة ثابتة (imports/strings/resources/layout)، لكنها **لا** تكشف الشيفرة المصدّرية или منطق UI (VLC مكتوب بـ C/C++).
- libVLC (libvlc.dll) موجود ضمن تثبيت VLC، وVLC.exe itself يربط `libvlc.dll` (ملاحظة string `libvlc.dll` ورموز `libvlc_new` 등).
- **الخطوة التالية (وحيدة):** اعتماد هذا التقرير/الخطة، ثم تثبيت .NET SDK (8 أو 9) و إنشاء هيكل solution minimal (بدون وظائف) في مرحلة منفصلة.

## 2. الأدلة (Observed facts)

| # | الملاحظة | المصدر | الثقة |
|---|-----------|--------|-------|
| F1 | `E:\App` فارغ (0 ملفات) | dir | observed |
| F2 | VLC 3.0.24 64-bit PE | REA `open_binary` | observed |
| F3 | SHA-256 لـ vlc.exe = `63d0d02df6e466d642b824dc66be6b5a134bf98b26747ffac8ec0d879dc180d7` | REA | observed |
| F4 | vlc.exe يحتوي 3773 procedure و 831 string (binary_overview) | REA | observed |
| F5 | مورد أيقونة `VLC_ICON` (RT_GROUP_ICON, 6 صور) embedded في vlc.exe | REA `inspect_pe_resources` | observed |
| F6 | strings: `libvlc.dll`, `libvlc_new`, `libvlc_release`, `libvlc_add_intf`, `libvlc_playlist_play`, `libvlc_set_app_id`, `libvlc_set_user_agent`, `libvlc_wait` | REA `search_strings` | observed |
| F7 | .NET Framework 4.8 مثبت | Registry | observed |
| F8 | لا dotnet SDK 현대 (لا `dotnet.exe`) | PATH/FS | observed |
| F9 | لا git | PATH | observed |

### 2.1 ما يمكن لـ REA فحصه فعليًا
- فتح/تحليل PE (VLC.exe): architecture, format, SHA256.
- binary_overview: procedure_count, string_count.
- inspect_pe_resources: resource tree, icon groups (VLC_ICON).
- search_strings: اكتشاف أسماء libvlc APIs واسم DLL.
- inspect_binary_layout ( ELF/PE layout): sections, symbols, needed libs (لم يُشغّل هنا لتجنب ضخامة، لكن الأداة متاحة).
- debug sessions (rizin/frida) — ممكنة but heavy; غير مطلوبة لمرحلة التخطيط.

### 2.2 ما لا يمكن لـ REA فعله (حدود)
- لا source code لـ VLC (C/C++ native) — REA تحلل bytes only; decompilation possible but not UI logic at high level.
- لا يكشف "سلوك" (play/pause UX) تلقائيًا؛ needs manual observation or official docs.
- لا يكشف تكوين WPF/WinForms (VLC GUI (Qt) not applicable).
- لا يتحقق من licensing/compatibility — needs official sources.

### 2.3 ما يحتاج ملاحظة يدوية / توثيق رسمي
- سلوك واجهة VLC: shortcuts, drag&drop, playlist interactions, track selection menus.
- VideoLAN docs for libVLC API (media player events, audio/subtitle tracks, seeking).
- LibVLCSharp docs (VideoView WPF, airspace issues).
- .NET SDK installation & WPF project templates.
- Licensing: VLC (GPLv2+) vs libVLC (LGPL) — using libVLC as engine is allowed; must comply with LGPL (dynamic linking). Need legal review.

## 3. قائمة الوظائف المرجعية و حالات اختبار

### 3.1 الوظائف (scope initial)
1. Open audio/video files (file dialog + drag&drop).
2. Play / Pause / Stop.
3. Volume + Mute.
4. Progress bar + seek.
5. Basic playlist.
6. Audio track selection.
7. Subtitle track selection.
8. Keyboard shortcuts + drag&drop.

### 3.2 حالات اختبار (test cases)
- TC01: فتح MP4/MKV/MP3/WAV via dialog → يبدأ التشغيل.
- TC02: سحب وإفلات ملف → يبدأ التشغيل.
- TC03: Play/Pause toggle (Space) → state correct.
- TC04: Stop → resets position to 0, UI idle.
- TC05: Volume slider 0–100 → audio level changes; Mute toggles.
- TC06: Seek via progress bar → position jumps; no crash.
- TC07: Seek during pause → resumes at new position.
- TC08: Playlist add/remove/reorder → plays next.
- TC09: Switch audio track (multi-audio file) → audio changes.
- TC10: Switch subtitle track → subtitles render.
- TC11: Keyboard shortcuts: Space, M, Up/Down volume, Left/Right seek, Esc fullscreen exit (if implemented).
- TC12: Invalid file → graceful error, no crash.
- TC13: Missing codec → graceful handling.
- TC14: Resource cleanup on close (no lingering libvlc process).
- TC15: Reopen after close → no handle leak.

## 4. بنية المشروع المقترحة و الحزم

### 4.1 Target framework
- .NET 8 (LTS) or .NET 9 with WPF (`UseWPF=true`). Requires .NET SDK install (not present now).
- Alternative (if SDK unavailable): .NET Framework 4.8 + WPF + LibVLCSharp 3.x (supported). But modern tooling preferred.

### 4.2 Solution structure
```
VlcRebuild.sln
src/
  VlcRebuild.App/          (WPF app, views, viewmodels)
  VlcRebuild.Media/        (libVLC wrapper, playback service)
  VlcRebuild.Playlist/     (playlist model/service)
tests/
  VlcRebuild.Tests/        (unit tests)
  VlcRebuild.Integration/  (media playback smoke tests)
docs/
  ANALYSIS_REPORT.md
```

### 4.3 Packages (proposed)
- `LibVLCSharp` (core bindings)
- `LibVLCSharp.WPF` (VideoView)
- `VideoLAN.LibVLC.Windows` (native libvlc binaries) — provides `libvlc.dll` etc.
- MVVM: `CommunityToolkit.Mvvm` (optional).
- Testing: `xunit`, `FluentAssertions`.

### 4.4 Compatibility notes
- Must match native libVLC architecture (x64) with app (x64).
- `VideoLAN.LibVLC.Windows` version must align with LibVLCSharp major.
- WPF airspace limitations: VideoView is HWND-based; overlay controls may not render on top. Mitigations: separate control layer, or use `D3D11` interop per LibVLCSharp docs.

## 5. المخاطر

### 5.1 عرض الفيديو في WPF
- Airspace issue: native HWND overlays block WPF controls.
- Black screen on some GPU/drivers.
- Fullscreen toggling complexity.
- High-DPI scaling.

### 5.2 إدارة موارد libVLC
- Must dispose `MediaPlayer`, `Media`, `LibVLC` properly (IDisposable).
- Event handlers leaks.
- Native memory not collected by GC; explicit cleanup needed.
- Multiple instances → resource exhaustion.

### 5.3 توزيع المكتبات الأصلية
- Native DLLs (libvlc.dll, plugins) must be copied to output and match version.
- Plugin path resolution (`--plugin-path`).
- Installer size large (~tens MB).
- Antivirus false positives possible.

### 5.4 قانوني/ trademarks
- Do not reuse VLC icons/trademarks. Create independent visual identity.
- libVLC is LGPL: dynamic linking okay; comply with license notices.

## 6. خطة مراحل (بدون تنفيذ الآن)

- Phase 0 (done): Environment exploration + report.
- Phase 1: Install .NET SDK 8/9; create empty solution + projects; add packages; build "Hello WPF" (no media).
- Phase 2: Integrate LibVLCSharp: initialize LibVLC, VideoView, play a file (smoke).
- Phase 3: Implement playback controls + volume + seek + progress.
- Phase 4: Playlist + drag&drop + shortcuts.
- Phase 5: Audio/subtitle track selection.
- Phase 6: Stability tests + resource leak tests + packaging.
- Phase 7: UI polish + independent branding.

## 7. الخطوة التالية (وحيدة)

اعتماد هذا التقرير/الخطة، ثم في مرحلة منفصلة: تثبيت .NET SDK 8 (أو 9) وإنشاء solution فارغة مع المشاريع (بدون وظائف) والتحقق من البناء. لا تُنفّذ أي وظيفة media قبل اعتماد Phase 1.
