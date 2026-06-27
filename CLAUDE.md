# CLAUDE.md — 콩대리(KongDaeri)

이 저장소는 Windows 데스크톱 AI 비서 펫 **콩대리**를 개발한다.
전체 기획·아키텍처·로드맵의 기준(ground truth)은 아래 문서다.

@docs/KongDaeri_PLAN.md

## 기술 스택
- C# / .NET 8 (LTS) / WPF — `<TargetFramework>net8.0-windows</TargetFramework>`, `<UseWPF>true</UseWPF>`
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
- **노션 토큰·Gemini 키를 소스에 하드코딩하거나 커밋하지 말 것.** 환경변수 또는 gitignore된 로컬 설정 사용.
- 비밀이 들어가는 설정 파일은 반드시 `.gitignore`에 추가.

## 현재 단계
- [ ] 1단계: 데스펫(투명 오버레이) + 클립보드 비서 + SQLite 저장 + 노션 전송
- [ ] 2단계: 화면 스니퍼(전역 단축키 + 영역 캡처 + Gemini 비전)
- [ ] 3단계: 데스펫 폴리싱(눈 깜빡임 / 바운스 / 상태별 포즈)
- 범위 밖(개선): 파일 정리 집사(제안/미리보기 방식), 데스펫 부위 리깅
