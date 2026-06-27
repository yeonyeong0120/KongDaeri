# CLAUDE.md — 콩대리(KongDaeri)

이 저장소는 Windows 데스크톱 AI 비서 펫 **콩대리**를 개발한다.
전체 기획·아키텍처·로드맵의 기준(ground truth)은 아래 문서다.

@docs/KongDaeri_PLAN.md

## 기술 스택
- C# / .NET 10 / WPF — `<TargetFramework>net10.0-windows</TargetFramework>`, `<UseWPF>true</UseWPF>` (개발 PC에 SDK 10만 설치됨)
- MVVM: CommunityToolkit.Mvvm
- DI: Microsoft.Extensions.DependencyInjection
- 저장소: SQLite (Microsoft.Data.Sqlite)
- AI: Google Gemini `gemini-2.5-flash-lite` (멀티모달)
- 아카이빙: Notion REST API — `POST /v1/pages`, `markdown` 파라미터, `Notion-Version: 2026-03-11`
- OS 제어: Win32 P/Invoke (전역 단축키 / 클립보드 / 투명 오버레이 / 화면 캡처)

## 빌드 / 실행
- 빌드: `dotnet build`
- 실행: `dotnet run --project src/KongDaeri`
- (또는 Visual Studio에서 .sln 열고 F5)

## 목표 폴더 구조
- `src/KongDaeri/` — WPF 앱 (App / Views / ViewModels)
- `src/KongDaeri/Core/` — 파이프라인: `ICaptureSource` `IStorage` `IAiProcessor` `IExporter` `CaptureItem`
- `src/KongDaeri/Sources/` — Clipboard / ScreenSnip / File 어댑터
- `src/KongDaeri/Interop/` — Win32 P/Invoke
- `docs/` — 기획서(plan.md)

## 작업 규칙
- **세로 슬라이스 우선**: 1단계(데스펫 + 클립보드 + SQLite + 노션)를 끝까지 작동시킨 뒤 다음 단계로 넘어간다.
- 작은 단위로 작업하고 **각 단계마다 `dotnet build`로 빌드 확인** 후 진행.
- 입력 소스 인터페이스(`ICaptureSource`)는 **출력(`CaptureItem`)과 생명주기(Start/Stop)만 통일**하고, 트리거 방식(이벤트/사용자/폴링)은 소스 내부 자유.
- 캡처 이벤트와 AI 처리는 분리(큐 + async) — AI가 실패/지연해도 저장은 즉시 완료.
- **클립보드 수집은 저장(Collected)까지만 자동, AI 정리는 수집함에서 수동 트리거**([AI 정리]/[노션 공유] 버튼). 매 복사마다 Gemini를 호출하지 않는다(무료 키 호출/노이즈 절감).
- **노션 토큰·Gemini 키를 소스에 하드코딩하거나 커밋하지 말 것.** 환경변수 또는 gitignore된 로컬 설정 사용.
- 비밀이 들어가는 설정 파일은 반드시 `.gitignore`에 추가.
- **민감정보 필터(텍스트 수집)**: 저장 직전 `SensitiveDataFilter` 통과 — 고위험(카드·주민·키)은 수집 차단, 중위험(비밀번호 키워드·이메일·전화)은 마스킹 저장. 마스킹 전 원본/차단 원문은 로그·DB에 남기지 않는다(차단 시 유형만). 보조로 일시정지 토글.
- AI/노션 시크릿은 `%LOCALAPPDATA%\KongDaeri\settings.json` 에 보관한다. 이 경로는 저장소 밖이라 커밋되지 않으며, 코드/저장소 내 파일에 키를 절대 하드코딩하지 않는다. (배포 시에는 이 파일 저장값을 DPAPI로 암호화하는 것을 권장 — MVP 범위 밖.)

## 현재 단계
- [ ] 1단계: 데스펫(투명 오버레이) + 클립보드 비서 + SQLite 저장 + 노션 전송
- [ ] 2단계: 화면 스니퍼(전역 단축키 + 영역 캡처 + Gemini 비전)
- [ ] 3단계: 데스펫 폴리싱(눈 깜빡임 / 바운스 / 상태별 포즈)
- 범위 밖(개선): 파일 정리 집사(제안/미리보기 방식), 데스펫 부위 리깅
