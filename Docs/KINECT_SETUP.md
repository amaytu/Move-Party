# Configuração do Kinect no Unity 6000.3.14f1

> **Este projeto usa o Kinect v1 (Xbox 360) — veja a Parte B.** A Parte A fica como referência caso troquem para o v2.
> O `ProjectSetup` (menu **Imitações**) liga o símbolo `KINECT_V1` automaticamente quando encontra `C:\Windows\System32\Kinect10.dll`.

> Resumo: o Kinect é opcional para compilar. Sem ele, o projeto roda com o `MockBodySource`.
> O código do sensor só compila quando o símbolo `KINECT_V2` estiver definido.

---

## Parte A — Kinect v2 (Xbox One) — recomendado

### A1. Hardware e drivers (fora da Unity)

1. **Adaptador:** o Kinect do Xbox One precisa do *Kinect Adapter for Windows* (fonte + USB).
2. **USB 3.0** de verdade, direto na placa-mãe (evite hubs). Controladoras Intel ou Renesas funcionam melhor; algumas ASMedia dão problema.
3. Instale o **Kinect for Windows SDK 2.0** (`KinectSDK-v2.0_1409-Setup.exe`, da Microsoft).
4. Teste **antes** de abrir a Unity:
   - Rode o **Kinect Configuration Verifier** (vem no SDK Browser). Tudo verde, exceto talvez o aviso de GPU.
   - Abra o exemplo **Body Basics-WPF** no SDK Browser e confirme que o esqueleto aparece.
   - Se não funcionar aqui, também não vai funcionar na Unity.

### A2. Baixar o plugin da Unity

Baixe da Microsoft o **KinectForWindows_UnityPro_2.0.1410.zip** ("Unity Pro packages").
O arquivo que importa é o `Kinect.2.0.1410.19000.unitypackage`. O zip também traz projetos de exemplo (`KinectView`, `GreenScreen`), úteis para testar.

### A3. Configurar o projeto ANTES de importar

Em **Edit > Project Settings > Player > Other Settings**:

| Opção | Valor | Por quê |
|---|---|---|
| Scripting Backend | **Mono** (durante o desenvolvimento) | Build mais rápido; IL2CPP também funciona com P/Invoke, mas deixe para depois |
| Api Compatibility Level | **.NET Standard 2.1** (padrão) | O wrapper é C# simples + P/Invoke. Se der erro de tipo não encontrado, troque para **.NET Framework** |
| Allow 'unsafe' Code | Deixe desmarcado aqui | O asmdef do wrapper (`Windows.Kinect.asmdef`) já habilita `unsafe` só para ele |
| Active Input Handling | **Input System Package (New)** | Padrão do Unity 6; o mock e as teclas de debug usam o Input System |

Em **File > Build Profiles**: plataforma **Windows**, arquitetura **Intel 64-bit** (x86_64). O plugin nativo do Kinect é só 64 bits (e ARM não é suportado).

### A4. Importar o pacote

1. **Assets > Import Package > Custom Package...** → `Kinect.2.0.1410.19000.unitypackage` → Import.
2. Se aparecer o *API Update Required*, aceite (**I Made a Backup. Go Ahead!**).
3. Arrume as pastas importadas:

   | Importado em | O que fazer |
   |---|---|
   | `Assets/Plugins/x86_64/KinectUnityAddin.dll` | **Manter** |
   | `Assets/Plugins/x86/...` (se existir) | **Apagar** (32 bits, não usamos) |
   | `Assets/Plugins/Metro/...` | **Apagar** (é UWP e causa erro de "plugins com o mesmo nome") |
   | `Assets/Standard Assets/Windows/Kinect/*.cs` e demais `.cs` do pacote | **Mover** para `Assets/ThirdParty/KinectV2/` (onde já está o `Windows.Kinect.asmdef`) |

   Por que mover: scripts em `Standard Assets` compilam no `Assembly-CSharp-firstpass`, e assemblies com `.asmdef` (como o nosso `Imitacoes.Kinect`) não conseguem referenciar esse assembly.

4. Selecione `Assets/Plugins/x86_64/KinectUnityAddin.dll` e, no Inspector (Plugin Import Settings):
   - **Include Platforms:** marque **Editor** e **Standalone**.
   - **Editor → CPU:** x86_64 · **OS:** Windows.
   - **Standalone → Windows x86_64:** marcado; x86 desmarcado.
   - **Apply.**

### A5. Ativar o nosso código do Kinect

**Project Settings > Player > Other Settings > Scripting Define Symbols** → adicione `KINECT_V2` → Apply.

Sem esse símbolo o `Imitacoes.Kinect.asmdef` é ignorado e o projeto compila normalmente, só com o mock. Útil para quem abrir o projeto em um PC sem o SDK.

### A6. Teste rápido

1. Abra a cena de exemplo do pacote (`KinectView`), se você a importou, ou aguarde o `KinectBodySource` da próxima etapa.
2. Dê Play com o sensor ligado: o LED branco do Kinect acende e o esqueleto aparece.
3. **Pare o Play Mode e dê Play de novo.** Se falhar na segunda vez, o sensor não foi fechado: o `BodySourceBehaviour` já chama `StopSource()` no `OnDisable` justamente por isso.

### A7. Problemas comuns

| Sintoma | Causa provável | Solução |
|---|---|---|
| `DllNotFoundException: KinectUnityAddin` | DLL sem a plataforma Editor/x86_64 marcada, ou SDK não instalado | Revise A4 passo 4; confirme o SDK (A1) |
| `Multiple plugins with the same name 'KinectUnityAddin'` | Sobrou a pasta `Metro` ou `x86` | Apague as duplicadas |
| `The type or namespace 'Windows' could not be found` no nosso código | Wrapper ainda em `Standard Assets` ou `.asmdef` ausente | Mova os `.cs` para `ThirdParty/KinectV2` |
| Erros de `unsafe` no wrapper | `.asmdef` do wrapper sem `allowUnsafeCode` | Já vem marcado no `Windows.Kinect.asmdef` fornecido |
| Erros de tipos .NET faltando no wrapper | API level | Troque para **.NET Framework** (A3) |
| `IsAvailable` fica `false` | USB 2.0, controladora incompatível, adaptador sem energia | Teste no Configuration Verifier; mude de porta |
| Funciona no Editor e não no build | Build em x86 ou DLL não copiada | Build Profiles em Intel 64-bit; confira `<Build>_Data/Plugins/x86_64/` |
| Frames "travados" ao rodar a 60+ fps | O Kinect entrega 30 fps | Normal: por isso a interface tem `FrameArrived`/`FrameNumber` |

---

## Parte B — Kinect v1 (Xbox 360) — alternativa

- Instale o **Kinect for Windows SDK 1.8** (e, se quiser, o Developer Toolkit 1.8 para testar com o *Skeleton Basics-WPF*).
- O Kinect do Xbox 360 precisa do adaptador de energia/USB próprio.
- **Não existe pacote oficial da Microsoft para a Unity no v1.** A abordagem mais robusta é chamar a API nativa `Kinect10.dll` (instalada no `System32` pelo SDK) por **P/Invoke** (`NuiInitialize`, `NuiSkeletonTrackingEnable`, `NuiSkeletonGetNextFrame`, `NuiShutdown`). Nada precisa ser copiado para `Plugins`.
- Usar o `Microsoft.Kinect.dll` gerenciado dentro da Unity é possível, mas depende do API level *.NET Framework* e costuma dar problema de carregamento; não recomendo.
- Diferenças que o nosso código já prevê:
  - 20 joints: não tem `Neck`, `HandTip` e `Thumb` (ver `SkeletonDefinition.ExistsInKinectV1`); `ShoulderCenter → SpineShoulder`, `Spine → SpineMid`, `HipCenter → SpineBase`.
  - No máximo 2 esqueletos completos; sem `HandState` (fica `Unknown`).
  - Mesma conversão de coordenadas: `unity = (x, y, -z)`.
- Se for usar o v1, eu crio um símbolo `KINECT_V1` e um `KinectV1BodySource` no mesmo formato do v2.

---

## Parte C — Sem sensor

Deixe `KINECT_V2` fora dos Scripting Define Symbols e use o `MockBodySource` (próxima etapa): ele reproduz gravações `.json` de `Assets/StreamingAssets/Recordings/` ou gera movimento sintético, e o cursor pode seguir o mouse.
