# Slimy Jam — Uygulama Planı

Bu klasör, `Assets/Slimy_Jam_GDD.md` dokümanındaki core mechanic'in **mevcut Snake Puzzle projesinden bağımsız** bir
implementasyonudur. Eski oyunun (`Assets/Scripts/`) hiçbir dosyasına dokunulmaz; sadece mimari yaklaşımı ve modül
alışkanlıkları örnek alınır.

## Mevcut projeden ne alındı / neyi değiştiriyoruz

| Konu | Snake Puzzle (mevcut) | Slimy Jam (yeni) |
|---|---|---|
| Hareket uzayı | `int[,] levelData` grid, 4 yön | Bağlı **logical node graph**, serbest derece |
| Pathfinding | `EasyPathManager` + `Square4DirPathFinder` (grid A*) | `PathfindingService` — graph A*, blocked-prefix ve reverse kuralı destekli |
| Blocking | Grid hücresinde 0/1 | `NodeOccupancyMap` (nodeId → occupant) |
| Hareket | `Snake.MovementStep` + DOTween per-unit tween | `RopeMovementController` — continuous progress + midpoint commit |
| Input | `DragAndDrop.HandleSnakeSelection` (1.5 unit radius) | `SlimyInputController` — aynı fat-finger mantığı, GDD'deki stabil tie-break ile |
| Level data | `LevelData` (tileData/snakeData/exitData…) | `SlimyLevelData` (groundNodes/ropes/holes) — GDD §12 şeması |
| Level generation | `LevelGenerator` JSON → prefab instantiate | `SlimyLevelGenerator` — aynı sıra: JSON → graph → holes → ropes → occupancy → spline → camera |
| Spline | Kullanılmıyor (Bezier/CatmullRom kendi kodu) | **Dreamteck Splines** `SplineComputer` + `SplineMesh`, `ISplineAdapter` arkasında |
| Kamera | `Modules.CameraManager.CameraController.AdjustCamera` | Aynı formül, graph bounds ile (`SlimyCameraController`) |

Dreamteck paketi `Assets/Plugins/Dreamteck/Splines` altında, `autoReferenced: true` bir asmdef ile geliyor —
runtime asmdef'imiz ona referans veriyor.

## Katmanlar

```
Data          SlimyLevelData / GroundNodeData / RopeData / HoleData  (JSON şeması, GDD §12)
              LevelDataValidator                                     (GDD §13.3 LOAD-02)

Core          GroundNode, GraphRepository (+SegmentSpatialHash), NodeOccupancyMap,
              TraversalRules, SlimyConfig (GDD §19 tunables)

Gameplay      RopeModel      — saf logical zincir (occupiedNodeIds)
              StepPlanner    — forward / reverse / hole adımını validate + commit (atomik, GDD §9.3)
              FreeEndpointChooser — açı öncelikli serbest uç seçimi (GDD §8.6)
              HoleModel, CollectionService (GDD §10)

Pathfinding   PathfindingService     — graph A*, own-body reverse zinciri, blocked prefix (GDD §7.3/7.4)
              PointerTargetResolver  — nearest segment + iki uçtan maliyet + hysteresis (GDD §7.1/7.2)

Movement      RopeMovementController — continuous progress, midpoint commit, cap, snap (GDD §8)

Presentation  Rope (MonoBehaviour), RopeVisual, ISplineAdapter → DreamteckSplineAdapter,
              Hole, GroundVisualBuilder, SlimyCameraController

Flow          SlimyGameManager (state), SlimyLevelGenerator, SlimyInputController
```

Bağımlılık yönü GDD §14.3 ile aynı: Input → Resolver → Pathfinding → Movement → RopeModel/Occupancy →
Collection → GameManager.

## Temel tasarım kararı: adım = (FromChain → ToChain) geçişi

GDD'deki forward ve reverse hareketi **tek bir** temsilde birleştiriyoruz. Her logical adım, aynı uzunlukta iki
zincir arasındaki geçiştir ve i. rope unit'i `Lerp(pos(From[i]), pos(To[i]), progress)` ile çizilir:

```
forward (head aktif):  [A,B,C,D] -> [X,A,B,C]
forward (tail aktif):  [A,B,C,D] -> [B,C,D,X]
reverse (head aktif):  [A,B,C,D] -> [B,C,D,E]     E = free endpoint'in seçtiği node
reverse (tail aktif):  [A,B,C,D] -> [E,A,B,C]
```

Bu sayede movement, blocking ve render tek kod yolundan geçer; `progress >= 0.5` (midpointThreshold) olduğunda
occupancy commit edilir (GDD §8.2), `progress == 1` olduğunda adım kapanır.

## Adımlar

1. Klasör + asmdef iskeleti
2. Data model + validator
3. Graph core: node, graph repository, segment spatial hash, occupancy map, traversal rules
4. RopeModel + FreeEndpointChooser + StepPlanner (atomik commit)
5. PathfindingService + PointerTargetResolver
6. RopeMovementController (continuous + midpoint + snap)
7. HoleModel + CollectionService + SlimyGameManager
8. SlimyInputController
9. Presentation: Rope/Hole MonoBehaviour, Dreamteck spline adapter, ground visual, camera
10. SlimyLevelGenerator + örnek level JSON'ları
11. Editor: SplineGraphBaker (spline → node graph bake), sahne kurucu, level authoring
12. Acceptance testleri (GDD §17) + derleme doğrulaması
