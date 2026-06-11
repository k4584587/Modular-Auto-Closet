---
title: 구버전에서 업그레이드
description: 구버전 AutoCloset 설정을 현행 구조로 마이그레이션하기.
---

:::note[작성 중]
이 페이지는 아직 작성 중입니다. (기획안 §5.19)
:::

## 자동 업그레이드

옷장 우클릭 → `Hirami > Upgrade Legacy Closet` → "구버전 옷장 감지" 다이얼로그 → "업데이트"

[사진: 구버전 옷장 감지 다이얼로그]

## 레거시 컴포넌트의 데이터 이관

레거시 컴포넌트(ClosetToggle/ClosetBlendshape)의 데이터는 Auto Apply Closet 또는 의상 추가로 ClosetConfig가 붙을 때 자동 이관됩니다.

(씬을 열 때의 자동 동작은 AutoClosetObjectToggle에 BlendshapeToggle이 추가되는 것뿐입니다.)
