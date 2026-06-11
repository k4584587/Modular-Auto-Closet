---
title: 5분 만에 옷장 만들기
description: 의상이 입혀진 아바타에서 옷장 완성, 테스트까지 최단 경로.
---

:::note[작성 중]
이 페이지는 아직 작성 중입니다. (기획안 §5.3)
:::

시작 전에 [체크리스트](../intro/#시작-전-체크리스트)를 확인하세요.

## 1. 옷장 만들기

아바타 안에 빈 GameObject를 만듭니다. 이름이 곧 인게임 메뉴 이름이 됩니다.

[사진: 아바타 하위에 "옷장" 오브젝트를 만든 하이어라키]

:::caution
반드시 **아바타 안**에 만드세요.
:::

## 2. 의상 넣기

[사진: 옷장 아래 의상 3벌이 자식으로 들어간 하이어라키]

:::tip
**첫 번째 자식이 기본 의상**(스폰 시 입고 있는 옷)이 됩니다.
:::

## 3. 적용

옷장 우클릭 → `Hirami > Auto Apply Closet`

[사진: 컨텍스트 메뉴에서 Hirami 서브메뉴가 펼쳐진 순간]

## 4. 결과 확인

[사진: 옷장 루트 Inspector — AutoCloset / MA Menu Installer / MA Parameters / MA Menu Item 4종이 붙은 모습]

[사진: Project 창 Assets/Hirami/AutoCloset/AutoCloset_xxxxxxxx/ 폴더의 자동 생성 썸네일들]

## 5. 테스트

[업로드 없이 테스트하기](../testing/)로 진행하세요.

[사진: 인게임 Expressions 메뉴에서 옷장 서브메뉴와 썸네일 토글들]

## 무슨 일이 일어났나요?

(옷장에 MA 메뉴/파라미터 자동 구성, saved 파라미터, 빌드 시 NDMF가 레이어 생성 — 작성 예정)
