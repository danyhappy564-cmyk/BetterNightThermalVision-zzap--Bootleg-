# Better Night Thermal Vision (fork)

> **원작자 · 원본**
> **ciallo** — https://forge.sp-tarkov.com/mod/2531/better-night-thermal-vision
> 아이디어 출처: [BetterThermal](https://sns.oddba.cn/104565.html) (GOOKLE, oddba)
>
> 이 레포는 위 원작의 **포크**입니다. 기능은 그대로고, **SPT 4.1에서 빌드·동작하도록
> 포팅**한 것이 전부입니다.

열상 조준경 / T7 열상 / 야간투시경의 노이즈·블러·글리치·픽셀화·프레임 제한 같은
"연출용 저하 효과"를 F12에서 끌 수 있게 해주는 플러그인입니다. 열상 색상(팔레트,
밝기, 색 편차)과 조준경 가시거리도 조절할 수 있습니다.

현재 기준 **SPT 4.1**.

---

## 4.1 포팅에서 바뀐 것

4.1은 클라이언트를 **역난독화**해서 배포합니다. 그래서 이 모드가 이름으로 붙잡고 있던
대상들이 두 부류로 갈립니다.

### 1. 이름이 바뀐 타입 — 그냥 새 이름으로 교체

| 4.0 | 4.1 | 쓰인 곳 |
|---|---|---|
| `CameraClass` | `EFT.CameraControl.CameraManager` | `BetterThermalNightVision.cs`, `NVScopeBright.cs` |
| `GClass3687` | `EFT.CameraControl.OpticCameraManager` | `NVScopeBright.cs` |

세 갈래로 교차 확인했습니다:

| 출처 | 결과 |
|---|---|
| assembly-tool `GClass-Mappings.json5` | `GClass3686` → `CameraManager`, `GClass3687` → `OpticCameraManager` (둘 다 `EFT.CameraControl`) |
| 4.0 어셈블리 메타데이터 | `PlayerCameraController`에 `CameraClass get_GClass3686_0()` 가 남아 있음 = `CameraClass` 가 곧 `GClass3686` |
| 이미 4.1로 올라간 다른 모드 | HollywoodFX / ManimalIcebreaker 가 `EFT.CameraControl.CameraManager.Instance.Camera` 를 그대로 사용 중 |

나머지 타입(`PlayerCameraController`, `OpticComponentUpdater`, `OpticSight`,
`ThermalVision`, `NightVision`, `ThermalVisionComponent`, `ThermalVisionUtilities`,
`ValuesCoefs`, `ProceduralWeaponAnimation`, `ChromaticAberration` …)은 원래부터 실명이라
리네임 표에 없습니다 = 그대로입니다.

### 2. 이름이 **사라진** 메서드 3개 — 이름 대신 모양으로 찾습니다

`method_*` 는 assembly-tool이 갈아엎는 접두사 목록에 들어 있어서, 4.1에는 이 이름들이
아예 없습니다. 게다가 새 이름은 **어디에도 공개돼 있지 않습니다** — assembly-tool은
빌드 중에 실명 레퍼런스 어셈블리와 시그니처를 맞춰서 이름을 지어내고, 그 결과를 표로
남기지 않습니다. 즉 "4.1 이름으로 바꿔 적기"가 불가능한 대상들입니다.

| 4.0 대상 | 하는 일 | 4.1에서 어떻게 처리했나 |
|---|---|---|
| `PlayerCameraController.method_5` | T7 열상 템플릿 재적용 | **로컬 변수 지문**으로 런타임 탐색 |
| `PlayerCameraController.method_4` | 야투 템플릿 재적용 | **로컬 변수 지문**으로 런타임 탐색 |
| `GClass3687.method_2` | 조준경이 활성 광학이 될 때 | **패치 제거** — 같은 지점의 `OnOpticEnabled` 이벤트 구독으로 대체 |

**로컬 변수 지문** (`VisionTargets.cs`): 두 메서드 다 `public void ()` 이고,
`PlayerCameraController` 에는 그런 메서드가 4개라 시그니처만으로는 구분이 안 됩니다.
구분이 되는 건 로컬 변수입니다 — 4.0 어셈블리 메타데이터를 뜯어보면:

```
Void method_1()  locals=[FaceShieldComponent]
Void method_2()  locals=[FaceShieldComponent]
Void method_4()  locals=[NightVision, NightVisionComponent, Boolean, ThermalVision]
Void method_5()  locals=[ThermalVision, ThermalVisionComponent, Boolean, NightVision, MaskDescription]
```

`NightVisionComponent` 로컬을 가진 메서드는 타입 전체에서 `method_4` 하나,
`ThermalVisionComponent` 로컬을 가진 건 `method_5` 하나뿐입니다. 둘 다 게임의 실명
아이템 컴포넌트 타입이라 역난독화 뒤에도 이름이 그대로고, 그래서 이 지문은 4.0/4.1
양쪽에서 똑같이 읽힙니다. 나중 빌드가 메서드 번호를 재배치해도 따라갑니다 —
`"method_4"` 를 박아뒀다면 조용히 엉뚱한 메서드를 패치했을 상황입니다.

로컬 변수로 한 개도 못 찾으면(컴파일러가 로컬을 접어버린 빌드) **메서드 본문이 그 타입을
언급하는지**로 한 번 더 훑습니다. 넓은 검사라 정밀한 쪽이 실패했을 때만 씁니다.

후보가 0개거나 2개 이상이면 **찍지 않고** BepInEx 로그에 에러를 남기고 해당 패치만
건너뜁니다.

**`OnOpticEnabled` 대체** (`NVScopeBright.cs`): `method_2` 가 마지막에 하는 일이 바로
그 이벤트를 쏘는 것이라, 구독이 후위 패치와 정확히 같은 시점에 실행됩니다. 패치가
아예 필요 없어졌습니다. `Init` 은 세션 중 여러 번 돌 수 있어서 구독 전에 해제를 한 번
겁니다(중복 방지).

### 3. `Material_0` — 이름이 바뀔 수 있어서 모양으로 찾습니다

`BSG.CameraEffects.NightVision.Material_0` 은 이름이 **자기 타입에서 파생된** 형태라,
assembly-tool 이 "난독화 잔재"로 보고 갈아치우는 조건에 걸립니다
(`IsReal()` = `!IsObfuscatedName() && !IsTypeDerived(memberType)`). 실제로 바뀌었는지는
4.1 어셈블리 없이는 확인이 안 돼서, `NightVision` 이 선언한 **유일한 `Material` 타입
프로퍼티**로 찾도록 했습니다. 옛 이름 `Material_0` 을 먼저 시도하므로 안 바뀌었으면
조회 한 번으로 끝납니다.

> 백킹 **필드**(`material_0`)는 일부러 쓰지 않았습니다. 게터가 머티리얼을 지연 생성해서,
> 누가 한 번 읽기 전까지 필드는 `null` 입니다.

---

## 빌드 설정도 갈아엎었습니다

- 구식 csproj → SDK 스타일, `v4.7.2` → `netstandard2.1`
- **하드코딩된 `D:\EFT408\...` 참조 경로 전부 제거.** 작성자 PC 한 대에서만 빌드되던
  프로젝트였습니다. `SptRoot` 로 대체했고 기본값은 `E:\SPT 4.1`,
  `-p:SptRoot=...` 또는 환경변수로 덮어쓸 수 있습니다
- `Properties\AssemblyInfo.cs` **Compile 항목 삭제** — 그 파일은 레포에 존재한 적이
  없습니다. 어셈블리 속성은 이제 csproj에서 생성합니다 (버전 1.3.2 유지)
- `SptRoot` 가 SPT 설치본이 아니면 "타입을 찾을 수 없음" 수십 줄 대신 **이유를 말하는
  에러 하나**로 실패합니다
- 빌드 후 `BepInEx\plugins\BetterThermalNightVision\` 로 DLL + `ColorRamp.png` 를 함께
  복사합니다. 플러그인이 `Info.Location` 기준으로 png를 읽기 때문에 **둘이 같은 폴더에
  있어야** 합니다
- 릴리스 zip을 압축 대상 폴더 **밖에** 쓰도록 staging 한 단계 아래에 만듭니다 (안에 쓰면
  자기가 쓰는 파일을 읽으려 들어서 MSB3931 "다른 프로세스에서 사용 중" 오류가 납니다)

---

## 설치

1. `Release\BetterNightThermalVision.zip` 을 SPT 설치 폴더에 풀거나,
   `BepInEx\plugins\BetterThermalNightVision\` 에 `BetterThermalNightVision.dll` 과
   `ColorRamp.png` 를 같이 넣습니다
2. **이전 버전이 `BepInEx\plugins\` 바로 아래에 낱개 DLL로 들어있으면 지우세요.**
   폴더 버전과 같이 있으면 GUID가 겹쳐서 BepInEx가 하나를 무시합니다
3. 설정은 게임 안에서 **F12**

## 빌드

```
dotnet build BetterThermal.csproj -c Release
dotnet build BetterThermal.csproj -c Release -p:"SptRoot=D:\내 SPT 경로"
```

---

## 확인한 것 / 확인 못 한 것

| | 상태 |
|---|---|
| 4.1 형태의 API 스텁을 만들어 전체 컴파일 | **통과** (경고는 원본 `ConfigurationManagerAttributes.cs` 것) |
| `method_4` / `method_5` 지문이 유일한지 | 4.0 어셈블리 메타데이터로 **확인** — 타입 전체에서 각각 1개 |
| 탐색 로직 자체 | **확인** — 4.1처럼 이름을 바꾸고 같은 시그니처 미끼를 섞은 테스트 타입에서 각각 정확히 1개를 골라냄 |
| `CameraClass` = `GClass3686` = `CameraManager` | **확인** (위 표 3갈래) |
| 실제 4.1 `Assembly-CSharp.dll` 로 컴파일 | **못 함** — 이 작업 환경에 4.1 클라이언트 어셈블리가 없습니다 |
| 인게임 레이드 검증 | **안 함** |

마지막 두 줄 때문에, 첫 실행 때 BepInEx 로그에서 다음 줄들을 확인해 주세요:

```
Patched PlayerCameraController.<이름> for T7 thermal settings.
Patched PlayerCameraController.<이름> for night vision noise.
Night-vision material property: <이름>
```

세 줄이 다 나오면 이름 해석이 전부 성공한 겁니다. 대신 `Could not find ...` 에러가
찍히면 그 항목만 동작하지 않는 상태이니 로그를 알려주세요.
