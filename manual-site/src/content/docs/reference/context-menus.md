---
title: 우클릭 메뉴 명령어
description: GameObject > Hirami 메뉴 4종의 기능과 활성 조건.
---

:::note[작성 중]
이 페이지는 아직 작성 중입니다. (기획안 §5.15)
:::

[사진(재사용): 퀵스타트의 Hirami 서브메뉴 컷]

| 메뉴 | 기능 | 활성 조건 |
|---|---|---|
| Auto Apply Closet | 옷장 생성 (다중 선택 시 각각 별도 옷장) | 아바타 본체가 아니고, 다른 옷장 안이 아니고, 하위에 AutoCloset이 없고, MA MenuItem/MeshSettings가 없을 때 |
| Add Create Toggle | 단독 토글 생성 | 씬에 옷장이 1개 이상 |
| Clear Closet Components | 옷장 컴포넌트 일괄 제거 (오브젝트는 보존, Undo 가능) | — |
| Upgrade Legacy Closet | 구버전 마이그레이션 | 구버전 컴포넌트 감지 시 |

## Component > Hirami 메뉴

[사진: Component > Hirami 메뉴 트리 (AutoCloset / ClosetConfig (Unified) / Legacy / Utility)]

## Clear Closet Components 상세

(다이얼로그 문구 원문 인용 — 작성 예정)

:::danger
옷장 하위의 MA Shape Changer가 모두 삭제됩니다 (의상 제작자가 넣은 기믹 포함).
:::
