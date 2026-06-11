---
title: 단독 토글 컴포넌트
description: AutoClosetObjectToggle, BlendshapeToggle, StandaloneToggle 레퍼런스.
---

:::note[작성 중]
이 페이지는 아직 작성 중입니다. (기획안 §5.14)
:::

## AutoClosetObjectToggle

[사진: 기본 Inspector]

씬을 열 때 BlendshapeToggle이 자동으로 함께 붙습니다 — 정상 동작입니다.

## BlendshapeToggle

[사진(재사용): 튜토리얼의 BlendshapeToggle 컷]

"켜짐" = 토글 ON일 때 값 적용, "꺼짐" = 토글 OFF일 때 값 적용.

## StandaloneToggle

아바타에 **이미 있는** Bool 파라미터에 오브젝트 on/off를 연동합니다.

[사진: 기본 Inspector]

자동 연결 규칙: 오브젝트 이름 기준 `Toggle_{이름}_` 접두사 → 시작 일치 → 포함 순으로 FX에서 검색, 없으면 새 파라미터 생성.

:::caution
현재 Component > Hirami 메뉴에 없습니다 — Add Component 검색으로 추가하세요.
:::
