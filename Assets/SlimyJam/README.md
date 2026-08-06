# Slimy Jam

`Assets/Slimy_Jam_GDD.md` dokümanındaki core mechanic'in implementasyonu. Mevcut Snake Puzzle projesinden
tamamen bağımsızdır — `Assets/Scripts/` altındaki hiçbir dosya değiştirilmemiştir.

## Hızlı başlangıç

1. `Assets/SlimyJam/Scenes/SlimyJam.unity` sahnesini açın — kurulu ve bağlanmış hâlde geliyor.
2. Play'e basın — `Resources/SlimyLevels/Level_1.json` yüklenir.
3. Başka bir level için `SlimyJam` nesnesindeki `SlimyLevelGenerator ▸ Level Resource Path` alanını
   `SlimyLevels/Level_3` gibi değiştirin.

Sahneler editor script'iyle üretilmiştir ve istendiğinde yeniden üretilebilir:

| Menü | Üretilen |
|---|---|
| **Tools ▸ Slimy Jam ▸ Create Playable Scene** | `Scenes/SlimyJam.unity` + `SlimyConfig.asset` |
| **Tools ▸ Slimy Jam ▸ Create Authoring Scene** | `Scenes/SlimyJam_Authoring.unity` |
| **Tools ▸ Slimy Jam ▸ Create All Scenes** | ikisi birden (batch modda da çalışır) |

```bash
Unity -batchmode -quit -projectPath . \
  -executeMethod SlimyJam.EditorTools.SlimyJamSceneBuilder.CreateAllScenes
```

> Sahneler prefab kullanmaz; zemin mesh'i, hole disk'i ve rope tube'ları runtime'da üretilir.
> Bu yüzden sahne dosyaları küçük ve merge-friendly'dir.

### Oynanabilir sahnenin içeriği

```
Main Camera        Camera (fov 45, pitch 55°) + SlimyCameraController
Directional Light
SlimyJam           SlimyGameManager + SlimyLevelGenerator + SlimyInputController
                     └ config → SlimyConfig.asset, gameManager/cameraController bağlı
```

Kontrol: rope'un **baş veya kuyruğuna** yakın basılı tutup sürükleyin. Aktif uç, parmağın graph üzerindeki
projeksiyonuna doğru pathfinding ile ilerler. Aktif ucu kendi gövdesine doğru sürüklerseniz karşı uç uzar
(reverse hareket). Uç matching hole'a girerse ya da bırakırken hole'a komşu node'da olursa rope toplanır.

## Klasör yapısı

```
Scripts/Runtime/
  Data/        SlimyLevelData, LevelDataValidator, SlimyLevelJson       — GDD 12, 13.3
  Core/        GroundNode, GraphRepository, SegmentSpatialHash,
               NodeOccupancyMap, TraversalRules, SlimyConfig            — GDD 3, 4, 9, 19
  Gameplay/    RopeModel, StepPlanner, FreeEndpointChooser,
               RopeMovementController, HoleModel, CollectionService,
               Rope, Hole                                               — GDD 5, 8, 9, 10, 11.3
  Pathfinding/ PathfindingService, PointerTargetResolver, PointerTarget  — GDD 7
  Input/       EndpointSelector, SlimyInputController                    — GDD 6
  Level/       SlimyLevelContext, SlimyLevelGenerator, SlimyGameManager,
               SlimyCameraController, Authoring/*                        — GDD 11, 13
  Visual/      ISplineAdapter, DreamteckSplineAdapter, GroundVisualBuilder,
               SlimyPalette, PolylineUtility                             — GDD 14.1
Scripts/Editor/  SplineGraphBaker, SlimyLevelAuthoringEditor, SlimyJamSceneBuilder
Scripts/Tests/   AcceptanceTests (GDD 17), TestLevelBuilder
Resources/SlimyLevels/  Level_1..5.json
```

## Temel tasarım kararı

GDD'deki forward ve reverse hareket **tek bir temsilde** birleştirildi. Her logical adım, aynı uzunlukta iki
zincir arasındaki geçiştir ve i. rope unit'i `Lerp(pos(From[i]), pos(To[i]), progress)` ile çizilir:

| Durum | From | To |
|---|---|---|
| forward, head aktif | `[A,B,C,D]` | `[X,A,B,C]` |
| forward, tail aktif | `[A,B,C,D]` | `[B,C,D,X]` |
| reverse, head aktif | `[A,B,C,D]` | `[B,C,D,E]` |
| reverse, tail aktif | `[A,B,C,D]` | `[E,A,B,C]` |

`progress >= midpointThreshold` olduğunda occupancy atomik olarak commit edilir (GDD 8.2, 9.3);
`progress == 1` olduğunda adım kapanır. Böylece movement, blocking ve render tek kod yolundan geçer.

Pathfinding, rope'un kendi gövdesini **yalnızca aktif uçtan içeri doğru sırayla** geçilebilir sayar
(`ownChainIndex[to] == ownChainIndex[from] + 1`), yani ortadaki bir body node'una dışarıdan atlanamaz (BLOCK-02).

## Level authoring akışı

1. **Tools ▸ Slimy Jam ▸ Create Authoring Scene** — `SlimyLevelAuthoring` kökü ve örnek Dreamteck spline'ları.
2. Yolları spline'larla çizin. Kesişen spline'ların kontrol noktaları aynı konuma denk gelmelidir;
   `weldTolerance` içindeki örnekler tek logical node'da birleşir (GDD 4.2 kural 4).
3. `RopeAuthoring` altına sıralı marker child'ları koyun (ilk child = head), `HoleAuthoring` nesnelerini yerleştirin.
4. Inspector'da **Validate (dry run)** ile uyarıları görün, sonra **Bake to JSON**.

Baker her spline'ın uzunluğunu tam sayı adet parçaya normalize eder; bir parça 1.0 world unit'ten belirgin
biçimde saparsa uyarı verir. Spline verisi runtime JSON'una **yazılmaz** (GDD 4.4).

## Level JSON formatı

```jsonc
{
  "levelIndex": 1,
  "groundNodes": [
    { "id": 100, "position": {"x":0,"y":0,"z":0}, "connectedNodeIds": [101] }
  ],
  "ropes": [ { "id": 1, "color": "Green", "occupiedNodeIds": [100,101,102] } ],
  "holes": [ { "id": 10, "color": "Green", "nodeId": 108 } ]
}
```

Renkler dosyada okunabilir isimle tutulur; `SlimyLevelJson.Parse` bunları JsonUtility için sayıya çevirir.
`LevelDataValidator` eksik node ID, tek yönlü connection, bağlı olmayan rope zinciri ve çakışan başlangıç
occupancy'sini yakalar; hata varsa gameplay başlatılmaz (GDD 13.3 / LOAD-02).

## Testler

`Window ▸ General ▸ Test Runner`:

| Assembly | Mod | İçerik |
|---|---|---|
| `SlimyJam.Tests` | EditMode | GDD 17 kabul senaryoları (SEL-01/02/03, PATH-02/04, MOVE-01..05, REV-01..04, BLOCK-01/02, HOLE-01..05, WIN-01, LOAD-01/02), sevk edilen level JSON'larının doğrulaması, üretilen sahnelerin bütünlüğü |
| `SlimyJam.PlayTests` | PlayMode | Uçtan uca bootstrap: generator graph/hole/rope + Dreamteck spline kuruyor mu, kuyruğu hole'a sürükleyince level kazanılıyor mu |

### ⚠️ Mevcut projede derleme engeli (SlimyJam ile ilgisi yok)

`com.lionstudios.release.lionads` ve `com.lionstudios.release.lionsdkservice` paketleri `AmazonAds`,
`APSInterstitialAdRequest`, `APSBannerAdRequest`, `APSVideoAdRequest` tiplerini bulamıyor:

```
Library/PackageCache/com.lionstudios.release.lionads@.../Runtime/AmazonAds.cs(2,7):
  error CS0246: The type or namespace name 'AmazonAds' could not be found
```

Sebebi: Amazon Publisher Services SDK'sı `Assets/Amazon/` altında **import edilmemiş .unitypackage** olarak
duruyor (`APS_Core_3_2_0.unitypackage`, `APSAppLovinMediation_3_1_0.unitypackage`). Bu paketler import
edilmeden proje derlenmiyor; dolayısıyla Unity Play moduna ve Test Runner'a giremiyor.

Bu durum SlimyJam'den önce de vardı — SlimyJam'in üç assembly'si (Runtime/Editor/Tests) uyarısız derleniyor.
Çözüm: iki `.unitypackage` dosyasını **Assets ▸ Import Package ▸ Custom Package** ile import edin.

Doğrulama için testler, yalnızca `Assets/SlimyJam` + `Assets/Plugins/Dreamteck` içeren izole bir Unity
projesinde çalıştırıldı: **33/33 test geçti** (Unity 6000.3.18f1, EditMode).

## Kapsam dışı (GDD ile aynı)

Timer, undo/restart/revive, deadlock detection, level editor kullanıcı akışı, art direction, UI, tutorial,
economy, monetization, audio, analytics ve meta progression.
