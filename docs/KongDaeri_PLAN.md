# 콩대리(KongDaeri) 기획서

> **한 줄 정체성** — 내 데스크톱을 떠돌며 화면·클립보드·파일에서 정보를 주워 담아, AI로 정리해 노션에 아카이빙해 주는 **AI 비서 펫**.

- **플랫폼**: Windows 전용 데스크톱 앱 (C# / WPF / .NET 8 LTS)
- **개발 방식**: Claude Code 기반 2일 집중 개발 (세로 슬라이스 MVP → 단계 확장)
- **브랜드명**: KongDaeri (한글 표기: 콩대리)

---

## 1. 기획 의도

### 1.1 문제 정의
리서치·업무 중 쓸 만한 정보는 늘 **여러 소스에 흩어진 채** 휘발된다 — 복사해 둔 텍스트, 화면 한 귀퉁이의 표, 다운로드 폴더의 파일. 이것들을 "나중에 정리해야지" 하다 잃어버리거나, 노션에 옮기는 수작업이 귀찮아 결국 안 한다.

### 1.2 해결 컨셉
콩대리는 데스크톱 위에 상주하는 **펫**의 모습으로, 흩어진 정보를 한 곳(로컬 수집함)에 모으고 → AI가 마크다운으로 정리 → 버튼 한 번에 노션 페이지로 아카이빙한다. 세 가지 입력 소스(클립보드 / 화면 스니퍼 / 파일 집사)가 **하나의 공유 파이프라인**으로 흘러 들어가는 것이 핵심이다.

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

public interface IAiProcessor      // Gemini 분류·요약·마크다운
{
    Task<CaptureItem> ProcessAsync(CaptureItem item);
}

public interface IExporter         // 내보내기 (Notion). 추후 확장 가능
{
    Task<string> ExportAsync(CaptureItem item); // 반환: 노션 page id
}
```

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
- **MVVM** (CommunityToolkit.Mvvm) + 간단한 **DI 컨테이너**(Microsoft.Extensions.DependencyInjection).
- 캡처 이벤트와 AI 처리를 **분리** → 캡처는 저장(Collected)까지만 자동, AI 처리는 수집함에서 **수동 트리거**(async). AI가 실패/지연돼도 저장은 이미 완료돼 있음.
- 노션 전송 실패 시 **재시도/보류 큐**.
- `IExporter` 추상화 덕에 추후 Obsidian 등 **다른 내보내기 대상 확장 용이**(확장성 어필 포인트).

---

## 4. WPF UI 구성

### 4.1 데스펫 오버레이 창
- `WindowStyle=None`, `AllowsTransparency=True`, `Background=Transparent`, `Topmost=True`, `ShowInTaskbar=False`
- 드래그로 이동, 위치 기억(설정에 저장)
- **에셋 전제**: 배경이 투명한 PNG 필요 (현재 시안은 흰 배경 → 1차 작업은 **배경 알파 제거**)

### 4.2 데스펫 애니메이션 (2일 기준 채택안)
- **기본**: 얼굴+안경 **통짜 투명 PNG** 1장
- **아이들 모션**: `Storyboard` + `TranslateTransform`(상하 바운스) + `RotateTransform`(좌우 흔들림) — "숨쉬는 느낌"
- **눈 깜빡임**: 눈을 벡터 `Ellipse`로 얹고 `ScaleY`를 잠깐 0 → 복귀
- **상태 반응**: **포즈 PNG 3~4장 교체** + 말풍선 텍스트(대기/수집/처리중/완료/에러)
- **개선사항(2차)**: 몸·팔·다리 부위 분리 후 `RotateTransform` 리깅으로 팔흔들기·걷기

### 4.3 상태 → 마스코트 매핑
| 상태 | 펫 표현 | 말풍선 예시 |
|---|---|---|
| 대기 | 아이들 바운스 + 가끔 깜빡임 | (없음) |
| 수집됨 | 잠깐 반짝/끄덕 | "주워 담았어요" |
| AI 처리중 | 생각하는 포즈 | "정리 중…" |
| 완료 | 기쁜 포즈 | "정리 끝!" |
| 에러 | 시무룩 포즈 | "앗, 실패했어요" |

### 4.4 부수 UI
- **시스템 트레이 아이콘**: 메뉴(수집함 열기 / 일시정지 / 설정 / 종료)
- **수집함 창**: 리스트/썸네일, 항목별 원본·AI결과 보기, **[노션 공유] 버튼**, 삭제
- **설정 창**: 노션 토큰·페이지, Gemini 키, 단축키, 제외 앱, 마스킹 토글, 일시정지
- (개선) 전체화면 앱 실행 시 펫 자동 숨김

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
- **이미지**: 스니핑 PNG는 앱 로컬 폴더에 저장, 경로만 DB에 기록. 썸네일 생성.
- **중복 제거**: 직전과 동일한 클립보드 텍스트는 무시(해시 비교).
- **동시성**: 클립보드 이벤트(UI 스레드) ↔ AI 비동기 처리 간 큐로 분리.
- **자동 만료(권장)**: 일정 기간 지난 미내보내기 항목 자동 정리 → "휘발성 수집함" 컨셉 + 프라이버시.

---

## 6. AI 연동 (Gemini)

### 6.1 모델
- **Gemini 2.5 Flash-Lite** (`gemini-2.5-flash-lite`) — 현재 가장 저렴한 멀티모달, 이미지 입력 지원, 무료 티어 존재.
- 텍스트 분류·요약 + 화면 캡처 이미지 인식을 **같은 모델**로 처리.

> **호출 시점(중요)**: AI 정리는 **매 캡처마다 자동 호출하지 않는다.** 수집함에서 사용자가
> [AI 정리] 또는 [노션 공유]를 누를 때만 `ProcessAsync`를 호출한다. 노션용 마크다운
> (`#제목`/`##부제목`) 구성을 위한 처리라 모든 복사에 호출할 필요가 없고, 무료 키 호출량과
> 노이즈(429 등)를 줄이기 위함.

### 6.2 역할별 프롬프트
- **분류/태깅**: 수집 항목의 주제·태그 추출
- **요약/정리**: 노션용 마크다운으로 재구성(제목 `#` 포함)
- **이미지 인식**: 스니핑 이미지에서 텍스트·표·요점 추출(OCR 대체/보완)

### 6.3 키 관리 (개발 단계)
- 개발 중 **본인 API 키** 사용.
- **하드코딩·커밋 금지** — 환경변수 또는 로컬 설정파일 + `.gitignore`.
- (배포 시 고려) 사용자 본인 키 입력 방식으로 전환.

### 6.4 실패/검증 정책
- AI 호출 실패해도 **캡처·저장은 유지**(상태만 Failed).
- 출력은 별도 검수 화면 없이 진행하되, 노션 전송 시 **alert**: "AI가 정리한 내용이라 부정확할 수 있어요. 노션에서 확인하세요."

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

| 도전 | 사용 API | 난이도 포인트 |
|---|---|---|
| 투명·항상위 오버레이 | `WS_EX_LAYERED` + WPF AllowsTransparency | 알파 PNG 전제, DPI 대응 |
| 클릭 통과 토글 (2차) | `SetWindowLong(GWL_EXSTYLE, WS_EX_TRANSPARENT)` | 펫 클릭 시엔 입력 받고 평소엔 통과 — 토글 타이밍 |
| 전역 단축키 | `RegisterHotKey` / `UnregisterHotKey` | 타 앱과의 키 충돌 처리 |
| 클립보드 감시 | `AddClipboardFormatListener` + `WM_CLIPBOARDUPDATE` | 메시지 루프 후킹, 중복/포맷 처리 |
| 화면 영역 캡처 | `Graphics.CopyFromScreen` / `BitBlt` | 멀티모니터 좌표 + DPI 스케일 변환 |
| 영역 선택 오버레이 | 전체화면 투명 창 + 드래그 사각형 | 좌표 정확도 |
| 활성 창 읽기 (2차) | `GetForegroundWindow` / `GetWindowText` | 출처 컨텍스트 기록 |

추가 도전: **캡처 이미지 → Gemini 비전 입력**으로 OCR을 대체/보완하는 멀티모달 연동.

---

## 9. 프라이버시 & 보안 정책

> 이 앱은 화면·클립보드·파일을 들여다보므로, 정책을 명시하는 것 자체가 성숙도 지표.

### 9.1 민감정보 마스킹 (베스트-에포트)
정책: **고위험 = 수집 차단 / 중위험 = 마스킹 저장.** (현재 텍스트 수집 경로에 적용. 스니핑 이미지는 범위 밖)
- **고위험(수집 차단)** — 항목 자체를 DB에 저장하지 않음:
  - 신용카드번호(13~16자리, 공백/하이픈 허용 + **Luhn 체크**로 오탐 감소)
  - 주민등록번호 패턴(6자리-7자리)
  - API 키/토큰류(`ntn_` / `sk-` / `AIza…` 등으로 시작하는 길이 임계값 이상 영숫자)
- **중위험(마스킹 저장)** — 값만 가리고 저장:
  - 이메일 → 로컬파트 일부만(`jo****@gmail.com`)
  - 전화번호(한국 형식 포함) → 가운데 자리 마스킹
  - 키워드 근접: `비밀번호/비번/password/pw/OTP/인증번호` 뒤 값(콜론·등호·공백 뒤 토큰)을 `****` 치환
- **입구에서 1회 필터**: 저장 직전 통과 → 이후 AI(Gemini)·노션으로도 마스킹본만 나감(원본 유출 방지). 마스킹 전 원본/차단 원문은 로그·DB 어디에도 남기지 않음(차단 시 **유형만** 기록).
- ※ **한계 명시**: 키워드/패턴 방식은 모든 민감정보를 잡지 못하고 오탐도 가능 → 보조 수단으로 **일시정지 토글**과 **앱별 제외**(2차) 제공.

### 9.2 로컬 저장 — 암호화 미적용 (의도된 범위 결정)
- 수집함은 **가벼운 휘발성 임시 보관소**로 설계(자동 만료). 영구·중요 보관은 **노션에 위임**.
- 따라서 로컬 DB는 암호화하지 않음(가벼운 읽기/쓰기 우선). 대신 마스킹·자동만료·사용자 삭제로 위험 최소화. → 이는 **누락이 아니라 명시적 설계 선택**.

### 9.3 외부 전송 고지
- AI 처리 시 내용이 Google(Gemini)로 전송됨을 설정/최초 실행 시 고지.

### 9.4 시크릿 관리
- 노션 토큰·Gemini 키는 평문 소스 노출 금지(환경변수/로컬 설정 + `.gitignore`).
- 키 저장 방식은 로컬 `settings.json`(`%LOCALAPPDATA%\KongDaeri\`)으로, 추후 설정 창에서 사용자가 본인 키를 입력하면 같은 파일에 기록되는 구조. 배포 단계에서 DPAPI 암호화로 확장 가능.

---

## 10. 개발 로드맵 (2일 / 단계별 세로 슬라이스)

### 1단계 — 세로 슬라이스 MVP (작동하는 데모의 뼈대)
데스펫(상태만 있는 단순 투명 창) + **클립보드 비서**(가장 쉬움, 텍스트 AI) + **로컬 저장(SQLite)** + **노션 전송**. → 끝까지 작동시키면 이미 완결된 데모.

### 2단계 — 화면 스니퍼 추가
영역 캡처 + 전역 단축키(Win32) + Gemini 비전 입력. → **시연 임팩트 큼.**

### 3단계 — 데스펫 폴리싱
눈 깜빡임 / 바운스 / 상태별 포즈 교체 / 말풍선. (클릭통과·활성창 읽기 P/Invoke 디테일은 시간 남으면.)

### 개선사항 (이번 범위 밖)
- **파일 정리 집사**: 파일 IO는 쉽지만 "내용 읽기"는 파서 필요, 이동/이름변경은 되돌리기 어려우니 **실제 변경 대신 "제안/미리보기"**로.
- 데스펫 부위 분리 리깅(팔다리 독립 모션).
- 사용자 본인 키 입력 / 다른 내보내기 대상(Obsidian 등).

---

## 11. 평가 항목 ↔ 대응 매핑

| 평가 항목 | 본 기획서 대응 |
|---|---|
| 기획 의도 | §1 문제정의·차별점, §2 시나리오 |
| WPF UI 구성 | §4 오버레이·데스펫·수집함·트레이·설정 |
| 데이터 처리 | §5 SQLite 스키마·중복제거·동시성 |
| 아키텍처 | §3 ICaptureSource 어댑터 + 파이프라인 + DTO |
| 차별화된 기술적 도전 | §8 Win32 P/Invoke + §6 멀티모달 |
| 라이브 시연 | §12 데모 대본 + 백업 플랜 |

---

## 12. 라이브 시연 계획

### 12.1 데모 대본
1. 콩대리 등장(투명 오버레이, 화면 위 떠다님)
2. 웹/문서에서 텍스트 복사 → 펫이 반응("주워 담았어요")
3. 단축키로 화면 표 영역 스니핑 → 펫이 처리 포즈
4. 수집함 열기 → 항목·AI 정리 결과 확인
5. **[노션 공유]** 클릭 → 펫 "정리 끝!"
6. 실제 노션 페이지가 깔끔하게 생성된 모습 확인

### 12.2 실패 대비 (의존성 많음)
- 노션/Gemini/네트워크 의존 → **사전 녹화 영상**, **목업 모드**(캐시된 AI 응답), 캐시 페이지 백업.
- 화면 공유 중 **API 키·토큰 노출 주의**.
- 노션 404 방지: 데모 페이지 **Connect 사전 확인**.

---

## 13. 기술 스택

- **언어/런타임**: C#, .NET 8 (LTS), WPF
- **MVVM/DI**: CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection
- **저장소**: SQLite (Microsoft.Data.Sqlite)
- **AI**: Google Gemini API — `gemini-2.5-flash-lite` (멀티모달)
- **아카이빙**: Notion REST API (`POST /v1/pages`, markdown 파라미터, `Notion-Version: 2026-03-11`)
- **OS 제어**: Win32 P/Invoke (전역 단축키 / 클립보드 / 오버레이 / 화면 캡처)

---

## 14. 리스크 & 대응

| 리스크 | 대응 |
|---|---|
| 2일 일정 촉박 | 1단계 세로 슬라이스 우선 완결, 나머지는 단계적 |
| 노션 404 (페이지 미연결) | 인티그레이션 Connect를 셋업 체크리스트로 강제 |
| 민감정보 유출 | 마스킹 + 일시정지 + 자동만료 (한계는 명시) |
| AI 부정확 | 저장은 분리, 노션에서 확인 안내 alert |
| 시연 중 네트워크/레이트리밋 | 녹화·목업·캐시 백업 |
| 데스펫 에셋 배경 | 1차로 알파 PNG 제작(배경 제거) |

---

*본 문서는 2일 Claude Code 개발을 전제로 한 실행용 기획서이며, 명시되지 않은 세부는 권장 기본값을 따른다.*
