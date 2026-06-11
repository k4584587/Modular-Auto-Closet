---
title: AutoCloset 컴포넌트
description: 옷장 루트 컴포넌트의 모든 Inspector 항목 — 언어, 토글 루트 이름, Write Defaults, 의상 기믹 파라미터 초기화.
---

:::note[작성 중]
이 페이지는 아직 작성 중입니다. (기획안 §5.12)
:::

[사진: AutoCloset 전체 Inspector (한국어, 로고부터 푸터까지)]

## Inspector 항목

| 항목 | 설명 |
|---|---|
| 언어 | English/Korean/Japanese — 하위 컴포넌트 Inspector 언어를 결정 |
| 토글 루트 이름 | Add Create Toggle이 만드는 토글 컨테이너 이름 (기본 `Toggle`) |
| Write Defaults | Auto·On·Off — 상세는 [WD 가이드](../write-defaults/) |
| 의상 기믹 파라미터 초기화 | 옷 전환 시 의상 내부 MA 기믹 파라미터를 기본값으로 복원 |

:::tip
언어·Write Defaults 드롭다운의 선택지는 한국어 모드에서도 영어로 표시됩니다 (Korean / Auto 등) — 정상입니다.
:::

[사진: Write Defaults 드롭다운을 펼친 상태 (Auto/On/Off)]

[사진: Auto일 때 표시되는 "아바타 FX에서 자동 감지: ON" HelpBox]

[사진: 아바타 밖에 배치했을 때의 "아바타 오브젝트 내부에 배치해 주세요." 에러 상태]

## 의상 기믹 파라미터 초기화 상세

(saved 파라미터 보호, `AutoClosetMenu_{이름}_{해시}` 자동 부여 — 작성 예정)

## PhysBone 보존 (v1.0.9, 자동)

설정 항목 없이 자동 동작합니다. (보존 조건, 직접 꺼둔 서브트리 제외 — 작성 예정)
