# 콩대리(KongDaeri) 기획서

> **한 줄 정체성** — 내 데스크톱을 떠돌며 화면·클립보드·파일에서 정보를 주워 담아, AI로 정리해 노션에 아카이빙해 주는 **AI 비서 펫**.

- **플랫폼**: Windows 전용 데스크톱 앱 (C# / WPF / **.NET 10**, `net10.0-windows`)
- **개발 방식**: Claude Code 기반 2일 집중 개발 (세로 슬라이스 MVP → 단계 확장)
- **브랜드명**: KongDaeri (한글 표기: 콩대리)
- **구현 현황**: 1·2·3단계(클립보드·SQLite·Gemini 정리/번역·노션 전송·화면 스니퍼·데스펫 표정) **구현 완료**. 파일 정리 집사·자동 만료·부위 리깅 등은 **범위 밖**(아래 §10·§4.2 참조).

---

## 1. 기획 의도

### 1.1 문제 정의
리서치·업무 중 쓸 만한 정보는 늘 **여러 소스에 흩어진 채** 휘발된다 — 복사해 둔 텍스트, 화면 한 귀퉁이의 표, 다운로드 폴더의 파일. 이것들을 "나중에 정리해야지" 하다 잃어버리거나, 노션에 옮기는 수작업이 귀찮아 결국 안 한다.

### 1.2 해결 컨셉
콩대리는 데스크톱 위에 상주하는 **펫**의 모습으로, 흩어진 정보를 한 곳(로컬 수집함)에 모으고 → AI가 마크다운으로 정리(또는 번역) → 버튼 한 번에 노션 페이지로 아카이빙한다. 입력 소스가 **하나의 공유 파이프라인**으로 흘러 들어가는 것이 핵심이다. (현재 **클립보드 / 화면 스니퍼** 구현, **파일 집사**는 2차.)

### 1.3 왜 모바일이 아니라 PC여야 하는가 (컨셉의 정당성)
이 앱의 핵심 기능은 PC 데스크톱 환경에서만 성립한다.
- **투명·항상위 오버레이 창** — 데스펫이 화면 위를 떠다니는 마스코트
- **전역 단축키 기반 화면 영역 캡처** — 어느 앱 위에서든 즉시 스니핑
- **클립보드 변경 감시** — 복사 행위를 실시간 감지
- **로컬 파일시스템 접근** — 파일 정리 집사

### 1.4 타깃 사용자
정보를 자주 수집·정리하는 지식 노동자 / 학생 / 개발자. 특히 **노션을 메인 아카이브로 쓰는 사람**.

### 1.5 차별점
| 기존 도구 | 한계 | 콩대리의 차별점 |
|---|---|---|
| Notion Web Clipper | 브라우저 안에서만, 웹페이지만 | OS 전역 + 클립보드/화면/파일 |
| 클립보드 매니저(Ditto 등) | 단순 저장, AI 정리 없음 | AI 분류·요약 후 노션 전송 |
| 스크린샷 툴(ShareX 등) | 캡처까지만 | 캡처 → 정리 → 아카이빙 일괄 |

세 입력 소스를 **하나의 어댑터 파이프라인 + 펫 UX**로 통합한 것이 정체성이다.

---

## 2. 핵심 기능 & 사용자 시나리오

### 2.1 핵심 흐름 (모든 소스 공통)
```
캡처 → 로컬 수집함 저장(원본, Collected)  [여기까지 자동]
      → (수집함에서 사용자가 [AI 정리] 또는 [노션 공유] 클릭 시) AI 처리(분류·요약·마크다운)
      → '노션 공유' → 노션 페이지
```
> AI 정리는 **자동이 아니라 수동 트리거**다. 캡처 시점엔 원본만 저장(Collected)하고,
> AI 정리는 수집함에서 사용자가 [AI 정리]/[노션 공유]를 누를 때 수행한다.
> (이유: AI 정리는 노션용 마크다운 `#제목`/`##부제목` 구성을 위한 것이라 매 복사마다
> 호출할 필요가 없고, 무료 키 호출/노이즈를 줄인다.)

### 2.2 입력 소스 3종
1. **클립보드 비서** (MVP) — 복사 이벤트를 감지해 텍스트를 수집함에 담는다.
2. **화면 스니퍼** (MVP) — 전역 단축키로 화면 영역을 드래그 캡처, 이미지를 AI 비전으로 인식.
   - 기본 단축키는 **`Ctrl+Alt+S`**, `settings.json` 의 `SnipHotkey` 로 변경 가능(추후 설정 창에서 노출).
3. **파일 정리 집사** (개선사항 / 2차) — 폴더를 감시·스캔해 파일을 분류·정리 *제안*. ※ 실제 이동·이름변경 대신 "제안/미리보기"로 안전하게.

### 2.3 대표 시나리오
> 자료 조사 중, 웹·PDF·메신저에서 쓸 만한 문장을 복사할 때마다 콩대리가 수집함에 담는다. 표가 있는 화면은 단축키로 스니핑한다. 조사가 끝나면 '노션 공유'를 누른다 → AI가 출처·요점별로 정리한 마크다운이 노션 한 페이지로 깔끔하게 생성된다.

---

## 3. 시스템 아키텍처

### 3.1 설계 원칙
세 입력 소스를 **동일한 `ICaptureSource` 인터페이스**로 추상화하고, 같은 뒷단(저장·AI·내보내기)에 어댑터로 꽂는다. 인터페이스는 **출력(공통 DTO)과 생명주기만 표준화**하고, 트리거 방식(이벤트 push / 사용자 트리거 / 폴링)은 각 소스 내부 자유에 맡긴다 → 어댑터가 억지로 끼워지지 않게.

### 3.2 공통 데이터 모델 (파이프라인의 표준 산출물)
```csharp
public enum CaptureSourceType { Clipboard, ScreenSnip, File }
public enum CaptureStatus { Collected, Processing, Processed, Exported, Failed }

public record CaptureItem
{
    public Guid Id { get; init; }
    public CaptureSourceType SourceType { get; init; }
    public DateTime CapturedAt { get; init; }

    // 원본
    public string? RawText { get; init; }       // 클립보드/파일 텍스트
    public string? ImagePath { get; init; }     // 스니핑 이미지 경로
    public string? SourceContext { get; init; } // 활성 창 제목, 파일 경로 등

    // AI 처리 결과
    public string? AiTitle { get; set; }
    public string? AiMarkdown { get; set; }
    public string[]? AiTags { get; set; }

    // 상태/내보내기
    public CaptureStatus Status { get; set; }
    public string? NotionPageId { get; set; }
}
```

### 3.3 인터페이스 (어댑터 + 파이프라인)
```csharp
// 입력 소스: 출력과 생명주기만 통일 (트리거는 내부 자유)
public interface ICaptureSource
{
    string Name { get; }
    CaptureSourceType Type { get; }
    event EventHandler<CaptureItem> Captured;   // 모든 소스가 push로 통일
    void Start();
    void Stop();
}

public interface IStorage          // SQLite 수집함
{
    Task SaveAsync(CaptureItem item);
    Task UpdateAsync(CaptureItem item);
    Task<IReadOnlyList<CaptureItem>> ListAsync();
    Task DeleteAsync(Guid id);
}

public enum AiTask { Organize, Translate }   // 정리 / 번역

public interface IAiProcessor      // Gemini 정리·번역·마크다운(텍스트·이미지 공통)
{
    Task<CaptureItem> ProcessAsync(CaptureItem item, AiTask task = AiTask.Organize, string? targetLanguage = null);
}

public interface IExporter         // 내보내기 (Notion). 추후 확장 가능
{
    Task<string> ExportAsync(CaptureItem item); // 반환: 노션 page id
}
```
> 구현체: `GeminiProcessor`(IAiProcessor), `NotionExporter`(IExporter), `SqliteStorage`(IStorage),
> `ClipboardSource`/`ScreenSnipSource`(ICaptureSource). 파이프라인 조정은 `CaptureService`가 담당.

### 3.4 데이터 흐름
```
ICaptureSource.Captured
        │  (CaptureItem 생성)
        ▼
  CaptureService ── 큐 ──▶ IStorage.SaveAsync   (Collected)   [자동: 여기까지]
        │
        ▼ (사용자가 수집함에서 '[AI 정리]' 또는 '[노션 공유]' 클릭)
  IAiProcessor.ProcessAsync ──▶ IStorage.Update  (Processed)
        │
        ▼ (이어서 '노션 공유'면 곧바로)
  IExporter.ExportAsync ──▶ IStorage.Update      (Exported)
        │
        ▼
  콩대리 상태 이벤트 ──▶ 데스펫 UI 반응
```

### 3.5 기술적 포인트
- **MVVM** (CommunityToolkit.Mvvm). DI 컨테이너는 쓰지 않고 **`App.xaml.cs`가 컴포지션 루트로 수동 배선**(MVP 규모에 충분).
- 캡처 이벤트와 AI 처리를 **분리** → 캡처는 저장(Collected)까지만 자동, AI 처리는 수집함에서 **수동 트리거**(async fire-and-forget). AI가 실패/지연돼도 저장은 이미 완료돼 있음.
- **일시 장애 재시도**: AI/노션 호출은 `RetryPolicy`(지수 백오프 + 지터, 429·5xx·네트워크 오류만 재시도, 401/404는 즉시 실패). 3회 실패 시 `Status=Failed`. (별도 "보류 큐"는 미구현)
- `IExporter` 추상화 덕에 추후 Obsidian 등 **다른 내보내기 대상 확장 용이**(확장성 어필 포인트).
- DPI: `app.manifest`로 **Per-Monitor V2** 선언(화면 캡처 좌표 정확도).

---

## 4. WPF UI 구성

### 4.1 데스펫 오버레이 창 (구현됨)
- `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost=True`, `ShowInTaskbar=False`.
- **고정 크기 창**(펫+말풍선 겹침 배치) → 말풍선이 떠도 펫 위치/크기가 흔들리지 않음.
- **좌클릭 드래그**로 이동, **더블클릭**=수집함 열기, **우클릭**=트레이와 동일한 컨텍스트 메뉴.
- 시작 위치: 첫 실행 시 작업영역 우측·세로 중앙 부근(DPI 논리좌표 기준). 이후 위치는 **`window.json`**(`%LOCALAPPDATA%\KongDaeri\`)에 저장·복원.
- 일시정지 중에는 펫을 흐리게(Opacity) 표시.
- **에셋**: 배경 투명 PNG(얼굴+안경) 사용.

### 4.2 데스펫 표정/애니메이션 (구현됨)
- **기본**: 얼굴+안경 통짜 투명 PNG 1장(`kongdr_clean_transparent.png`).
- **눈 깜빡임**: 대기 중 3~6초 랜덤 간격으로 blink PNG(`kongdr_blink.png`)로 잠깐(~120ms) 교체 후 복귀(`DispatcherTimer`).
- **상태 표정 교체**: 실패 시 sad(`kongdr_sad.png`), 성공 시 happy(`kongdr_happy.png`)로 잠시 전환 후 기본 복귀. 표정 교체는 같은 `Image`의 `Source`만 바꿔 위치 불변.
- **말풍선**: 상태별 텍스트(수집/정리·번역중/끝/실패)를 1.5초 표시.
- **개선사항(2차, 미구현)**: 아이들 바운스/흔들림(`Storyboard`+`Transform` "숨쉬는 느낌"), 몸·팔·다리 부위 분리 리깅.

### 4.3 상태 → 마스코트 매핑 (구현됨)
| 상태 | 펫 표현 | 말풍선 예시 |
|---|---|---|
| 대기 | 가끔 눈 깜빡임(blink) | (없음) |
| 수집됨 | 기본 | "콩대리가 주워 담았어요" / "찰칵! 콩대리가 주워 담았어요"(스니핑) |
| AI 처리중 | 기본 | "콩대리가 정리 중…" / "콩대리가 번역 중…" |
| 완료 | happy 표정 | "콩대리가 정리했어요!" / "콩대리가 번역했어요!" / "콩대리가 노션에 올렸어요!" |
| 에러 | sad 표정 | "앗, 실패했어요" |

### 4.4 부수 UI (구현됨)
- **시스템 트레이 아이콘**(WinForms `NotifyIcon`, 전용 `.ico`): 메뉴 = 수집 개수(표시) / 수집함 열기 / 설정 / 수집 일시정지(체크) / 콩대리 보이기·숨기기 / 마지막 항목 노션 전송 / 종료. **펫 우클릭 시 동일 메뉴**.
- **수집함 창**: 좌측 리스트(제목·시각·출처·상태 배지·이미지 썸네일, 최신순, **실시간 갱신**) + 우측 상세(원본 텍스트/이미지 + AI 결과: 제목·태그·마크다운, 처리 방식 배지). 액션 버튼 **[AI 정리] / [번역(언어)] / [노션 공유] / [삭제]**, 상단 [설정] 버튼.
- **설정 창**: Gemini 키·Notion 토큰(**PasswordBox 마스킹 + 보기 토글**, 빈칸 저장 시 기존값 유지) / Notion 페이지 ID(도움말·설명 아이콘) / **캡처해서 수집하기 단축키** / **번역 기본 언어** 드롭다운 / **[모든 수집 데이터 삭제]**(확인 후 captures+snips 삭제, 설정 보존). 저장 시 즉시 반영(처리기 교체·단축키 재등록).
- **폰트**: 수집함·설정 창 UI에 **Pretendard**(Regular/Bold) 적용. 단, 원본/AI결과 표시 영역은 가독성 위해 기존 폰트 유지.
- **민감정보 일시정지 토글**: 트레이 메뉴에서 클립보드 수집 일시정지/재개.
- (개선, 미구현) 전체화면 앱 실행 시 펫 자동 숨김 / 앱별 제외.

---

## 5. 데이터 처리 & 저장

### 5.1 저장소: SQLite (`Microsoft.Data.Sqlite`)
가볍게 읽고 쓰면서 검색·필터·중복제거가 가능. (단순화가 더 급하면 JSON 파일로 대체 가능하나, "데이터 처리" 측면에서 SQLite 권장.)

### 5.2 스키마 (개념)
```sql
CREATE TABLE captures (
    id            TEXT PRIMARY KEY,
    source_type   INTEGER NOT NULL,
    captured_at   TEXT NOT NULL,
    raw_text      TEXT,
    image_path    TEXT,
    source_context TEXT,
    ai_title      TEXT,
    ai_markdown   TEXT,
    ai_tags       TEXT,          -- JSON 배열 문자열
    status        INTEGER NOT NULL,
    notion_page_id TEXT
);
```

### 5.3 처리 정책
- **이미지**(구현): 스니핑 PNG는 `%LOCALAPPDATA%\KongDaeri\snips\`에 저장, 경로만 DB에 기록. 썸네일(`*_thumb.png`)도 생성.
- **중복 제거**(구현): 직전과 동일한 클립보드 텍스트는 무시(SHA-256 해시 비교).
- **동시성**(구현): 클립보드 이벤트(UI 스레드) 즉시 저장 ↔ AI는 수동 트리거 시 async 처리로 분리.
- **저장 경로**: SQLite DB는 `%LOCALAPPDATA%\KongDaeri\kongdaeri.db`.
- **자동 만료**(개선사항, 미구현): 일정 기간 지난 미내보내기 항목 자동 정리 → "휘발성 수집함" 컨셉. 현재는 사용자가 [삭제] 또는 설정의 [모든 데이터 삭제]로 직접 정리.

---

## 6. AI 연동 (Gemini)

### 6.1 모델
- **Gemini 2.5 Flash-Lite** (`gemini-2.5-flash-lite`) — 현재 가장 저렴한 멀티모달, 이미지 입력 지원, 무료 티어 존재.
- 텍스트 분류·요약 + 화면 캡처 이미지 인식을 **같은 모델**로 처리.
- **AI 처리 동작 2종**: '정리'(노션용 마크다운 정리)와 '번역'(대상 언어로 번역 후 노션용 마크다운). 번역 기본 언어는 설정에서 선택(기본 영어). 텍스트/이미지(비전) 경로 모두 지원.

> **호출 시점(중요)**: AI 정리는 **매 캡처마다 자동 호출하지 않는다.** 수집함에서 사용자가
> [AI 정리] 또는 [노션 공유]를 누를 때만 `ProcessAsync`를 호출한다. 노션용 마크다운
> (`#제목`/`##부제목`) 구성을 위한 처리라 모든 복사에 호출할 필요가 없고, 무료 키 호출량과
> 노이즈(429 등)를 줄이기 위함.

### 6.2 역할별 프롬프트 (구현됨)
- **정리(분류·요약)**: 주제·태그 추출 + 노션용 마크다운 재구성(맨 위 `# 제목` 포함). 출력은 `{title, tags, markdown}` JSON(`responseMimeType=application/json`)으로 받아 파싱.
- **번역**: 설정의 기본 언어(기본 English)로 번역 후 같은 JSON 형식의 마크다운 구성. 텍스트·이미지 양쪽 지원.
- **이미지 인식(비전)**: 스니핑 PNG를 base64 `inline_data`(image/png)로 전송해 텍스트·표·요점 추출(OCR 대체/보완). 정리/번역 동일 적용.

### 6.3 키 관리 (구현됨)
- 키/토큰은 **`%LOCALAPPDATA%\KongDaeri\settings.json`**에 보관(저장소 밖, 커밋 안 됨). 소스 하드코딩·커밋 금지(`.gitignore` 처리).
- **설정 창에서 사용자가 직접 입력**(마스킹 표시, 빈칸 저장 시 기존값 유지). 저장 즉시 반영.
- (배포 시 확장) 저장값 **DPAPI 암호화** — 현재 MVP 범위 밖.

### 6.4 실패/검증 정책
- AI 호출 실패해도 **캡처·저장은 유지**(상태만 Failed). 일시 장애는 `RetryPolicy`로 재시도(§3.5).
- **처리 실패 시 원인을 분류해 사용자에게 alert으로 안내**(키 오류 / 한도 초과 / 노션 미연결 / 네트워크 등). 안전 문구만 표시하고 키·토큰 평문은 노출하지 않음.
- 실패 시 **sad 표정·말풍선은 다른 상태보다 약 2초 더 지속**(평소 1.5초 → 실패 약 3.5초).

---

## 7. Notion 연동

### 7.1 방식
- **POST `/v1/pages`** 에 `children` 대신 **`markdown` 파라미터**로 마크다운 문자열 전송 → 페이지 생성.
- AI가 만든 마크다운을 그대로 보내면 노션이 블록으로 렌더링.

### 7.2 반드시 지켜야 할 전제 (체크리스트)
- [ ] **API 버전 헤더**: `Notion-Version: 2026-03-11` (markdown 파라미터는 이 버전부터 지원)
- [ ] **Internal Integration Token** 발급
- [ ] **대상 페이지를 인티그레이션에 Connect** (안 하면 페이지가 존재해도 **404**) ← 데모 직전 단골 사고
- [ ] 권한: `insert_content`, `insert_property` capability
- [ ] `markdown`은 `children`/`content`와 **동시 사용 불가**(상호 배타)
- [ ] `properties.title` 생략 시 첫 `# h1`이 페이지 제목으로 추출됨

### 7.3 요청 예시(개념)
```http
POST https://api.notion.com/v1/pages
Authorization: Bearer {NOTION_TOKEN}
Notion-Version: 2026-03-11
Content-Type: application/json

{
  "parent": { "page_id": "..." },
  "markdown": "# 제목\n정리된 본문...\n## 출처\n- ..."
}
```

---

## 8. 차별화된 기술적 도전 (Win32 P/Invoke)

> 평가 대비: 각 항목을 "왜 어렵고 어떻게 풀었는지"로 서술할 것.

| 도전 | 사용 API | 상태 | 비고 |
|---|---|---|---|
| 투명·항상위 오버레이 | WPF `AllowsTransparency` + `WindowStyle=None`, `Topmost` | ✅ 구현 | 알파 PNG, 고정 크기로 표정 교체 시 흔들림 없음 |
| 전역 단축키 | `RegisterHotKey` / `UnregisterHotKey` + `WM_HOTKEY` 훅 | ✅ 구현 | 기본 `Ctrl+Alt+S`, 설정에서 변경(재등록), `MOD_NOREPEAT` |
| 클립보드 감시 | `AddClipboardFormatListener` + `WM_CLIPBOARDUPDATE` | ✅ 구현 | HwndSource 메시지 훅, 텍스트만, 해시 중복 제거 |
| 화면 영역 캡처 | `Graphics.CopyFromScreen` | ✅ 구현 | **DPI 150% 좌표 변환**(Per-Monitor V2), 가상 화면(`SM_*VIRTUALSCREEN`) 기준 |
| 영역 선택 오버레이 | 가상 화면 전체 투명 창 + 드래그 사각형(EvenOdd 마스크) | ✅ 구현 | DIP→물리픽셀 환산, 캡처 직전 오버레이 숨김 |
| 클릭 통과 토글 | `SetWindowLong(GWL_EXSTYLE, WS_EX_TRANSPARENT)` | ⬜ 2차(미구현) | 평소 통과 / 펫 클릭만 입력 |
| 활성 창 읽기 | `GetForegroundWindow` / `GetWindowText` | ⬜ 2차(미구현) | 클립보드 항목 출처 컨텍스트 기록 |

추가 도전(✅ 구현): **캡처 이미지 → Gemini 비전(base64 inline_data) 입력**으로 OCR을 대체/보완하는 멀티모달 연동(텍스트·표 추출, 정리/번역).

---

## 9. 프라이버시 & 보안 정책

> 이 앱은 화면·클립보드·파일을 들여다보므로, 정책을 명시하는 것 자체가 성숙도 지표.

### 9.1 민감정보 마스킹 (베스트-에포트)
정책: **고위험 = 수집 차단 / 중위험 = 마스킹 저장.** (현재 텍스트 수집 경로에 적용. 스니핑 이미지는 범위 밖)
- **고위험(수집 차단)** — 항목 자체를 DB에 저장하지 않음:
  - 신용카드번호(13~16자리, 공백/하이픈 허용 + **Luhn 체크**로 오탐 감소)
  - 주민등록번호 패턴(6자리-7자리)
  - API 키/토큰류: 알려진 접두사(`AIza` / `AQ.` / `ntn_` / `secret_` / `sk-` / `sk-ant-` / `gsk_` / `ghp_` / `github_pat_` / `xox*-`) + 길이 임계값. 접두사는 **토큰 시작에서만** 매칭(`task-…` 같은 단어 중간 오탐 방지).
  - 접두사 없는 키도 **보수적 휴리스틱**으로 차단: 30자+ 공백없는 토큰, 대/소문자+숫자 혼합, 순수16진수(해시) 아님, **샤논 엔트로피 ≥ 3.6**. (URL·경로·한글·일반 문장·깃 해시는 통과 — 오탐 최소화)
- **중위험(마스킹 저장)** — 값만 가리고 저장:
  - 이메일 → 로컬파트 일부만(`jo****@gmail.com`)
  - 전화번호(한국 형식 포함) → 가운데 자리 마스킹
  - 키워드 근접: `비밀번호/비번/password/pw/OTP/인증번호` 뒤 값(콜론·등호·공백 뒤 토큰)을 `****` 치환
- **입구에서 1회 필터**: 저장 직전 통과 → 이후 AI(Gemini)·노션으로도 마스킹본만 나감(원본 유출 방지). 마스킹 전 원본/차단 원문은 로그·DB 어디에도 남기지 않음(차단 시 **유형만** 기록).
- ※ **한계 명시**: 키워드/패턴 방식은 모든 민감정보를 잡지 못하고 오탐도 가능 → 보조 수단으로 **일시정지 토글**과 **앱별 제외**(2차) 제공.

### 9.2 로컬 저장 — 암호화 미적용 (의도된 범위 결정)
- 수집함은 **가벼운 임시 보관소**로 설계. 영구·중요 보관은 **노션에 위임**.
- 따라서 로컬 DB는 암호화하지 않음(가벼운 읽기/쓰기 우선). 대신 **민감정보 필터(차단/마스킹)·사용자 삭제([삭제]/[모든 데이터 삭제])**로 위험 최소화. → 이는 **누락이 아니라 명시적 설계 선택**. (자동 만료는 §5.3대로 개선사항)

### 9.3 외부 전송 고지
- AI 처리 시 내용이 Google(Gemini)로 전송됨을 설정/최초 실행 시 고지.

### 9.4 시크릿 관리
- 노션 토큰·Gemini 키는 평문 소스 노출 금지(환경변수/로컬 설정 + `.gitignore`).
- 키 저장 방식은 로컬 `settings.json`(`%LOCALAPPDATA%\KongDaeri\`)으로, 추후 설정 창에서 사용자가 본인 키를 입력하면 같은 파일에 기록되는 구조. 배포 단계에서 DPAPI 암호화로 확장 가능.

---

## 10. 개발 로드맵 (단계별 세로 슬라이스) — 진행 현황

### 1단계 — 세로 슬라이스 MVP ✅ 완료
데스펫(투명 오버레이) + **클립보드 비서** + **로컬 저장(SQLite)** + **Gemini 정리/번역** + **노션 전송**. 추가로 수집함 창·설정 창·재시도 안정화·민감정보 필터까지.

### 2단계 — 화면 스니퍼 ✅ 완료
영역 선택 오버레이 + 전역 단축키(Win32) + DPI 좌표 변환 + **Gemini 비전(텍스트·표 추출)** + 정리/번역.

### 3단계 — 데스펫 폴리싱 ✅ 완료(부분)
눈 깜빡임(blink) / 상태 표정(sad·happy) / 말풍선 / 펫 우클릭 메뉴 / Pretendard 폰트. (아이들 바운스·부위 리깅은 미구현.)

### 추가 구현 ✅ (원래 "개선"이었으나 완료)
- **번역 동작**(정리/번역 2종, 기본 언어 설정).
- **사용자 본인 키 입력**(설정 창에서 키·토큰·단축키·번역언어 관리).
- **민감정보 필터**(차단/마스킹) + 일시정지 토글.

### 개선사항 (이번 범위 밖)
- **파일 정리 집사**: 내용 읽기 파서 필요, 이동/이름변경은 되돌리기 어려우니 **실제 변경 대신 "제안/미리보기"**로.
- 자동 만료, 데스펫 부위 분리 리깅(팔다리), 아이들 바운스, 클릭 통과 토글, 활성 창 출처 기록, 전체화면 자동 숨김, 앱별 제외, 키 DPAPI 암호화, 다른 내보내기 대상(Obsidian 등).

---

## 11. 평가 항목 ↔ 대응 매핑

| 평가 항목 | 구현 대응 |
|---|---|
| 기획 의도 | §1 문제정의·차별점, §2 시나리오 |
| WPF UI 구성 | §4 — 데스펫 오버레이(표정·우클릭메뉴) + 트레이 + 수집함 창(리스트·상세·실시간갱신) + 설정 창(마스킹·번역언어·도움말) + Pretendard |
| 데이터 처리 | §5 — SQLite(`%LOCALAPPDATA%`) 스키마·해시 중복제거·async 분리, 이미지 파일+썸네일 |
| 아키텍처 | §3 — `ICaptureSource`/`IStorage`/`IAiProcessor`/`IExporter` 어댑터 + `CaptureService` 파이프라인 + DTO, 수동 배선 |
| 차별화된 기술적 도전 | §8 Win32 P/Invoke(오버레이·전역단축키·클립보드·DPI 화면캡처) + §6 Gemini 멀티모달(비전) + §9 민감정보 필터 + §3.5 재시도 |
| 라이브 시연 | §12 데모 대본 + 백업 플랜 |

---

## 12. 라이브 시연 계획

### 12.1 데모 대본
1. 콩대리 등장(투명 오버레이, 화면 위 떠다님)
2. 웹/문서에서 텍스트 복사 → 펫이 반응("주워 담았어요")
3. 단축키로 화면 표 영역 스니핑 → 펫이 처리 포즈
4. 수집함 열기 → 항목·AI 정리 결과 확인
5. **[노션 공유]** 클릭 → 펫 "노션에 올렸어요!"
6. 실제 노션 페이지가 깔끔하게 생성된 모습 확인

### 12.2 실패 대비 (의존성 많음)
- 노션/Gemini/네트워크 의존 → **사전 녹화 영상**, **목업 모드**(캐시된 AI 응답), 캐시 페이지 백업.
- 화면 공유 중 **API 키·토큰 노출 주의**.
- 노션 404 방지: 데모 페이지 **Connect 사전 확인**.

---

## 13. 기술 스택

- **언어/런타임**: C#, **.NET 10** (`net10.0-windows`), WPF (`UseWPF` + `UseWindowsForms` — 트레이 `NotifyIcon`·`System.Drawing` 캡처용)
- **MVVM**: CommunityToolkit.Mvvm 8.4.2. **DI 컨테이너 미사용**(`App.xaml.cs` 수동 배선)
- **저장소**: SQLite — Microsoft.Data.Sqlite 10.0.9 + SQLitePCLRaw.bundle_e_sqlite3 3.0.3
- **AI**: Google Gemini API — `gemini-2.5-flash-lite` (멀티모달, REST 직접 호출). 정리 + 번역
- **아카이빙**: Notion REST API (`POST /v1/pages`, markdown 파라미터, `Notion-Version: 2026-03-11`)
- **OS 제어**: Win32 P/Invoke (전역 단축키 / 클립보드 / 오버레이 / 화면 캡처), `app.manifest` Per-Monitor V2
- **폰트**: Pretendard(Regular/Bold, 앱 리소스)
- **아이콘/표정 에셋**: 콩대리 PNG(기본/blink/sad/happy), 안경 `.ico`

---

## 14. 리스크 & 대응

| 리스크 | 대응 |
|---|---|
| 2일 일정 촉박 | 1단계 세로 슬라이스 우선 완결, 나머지는 단계적 |
| 노션 404 (페이지 미연결) | 인티그레이션 Connect를 셋업 체크리스트로 강제 |
| 민감정보 유출 | 고위험 차단 + 중위험 마스킹 + 일시정지 토글 + 사용자 삭제 (한계는 명시; 자동만료는 개선) |
| AI/노션 처리 실패 | 저장은 AI와 분리(실패해도 수집 유지) + 원인 분류 alert 안내 + sad 표정 2초 더 지속 |
| 시연 중 네트워크/레이트리밋 | 녹화·목업·캐시 백업 |
| 데스펫 에셋 배경 | 1차로 알파 PNG 제작(배경 제거) |

---

*본 문서는 Claude Code 기반 개발 기획서로, 현재 **실제 구현 상태에 맞춰 갱신**되었다. "구현됨/완료"는 코드 반영분, "개선사항/2차/미구현"은 범위 밖 항목이다.*
