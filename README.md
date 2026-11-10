# Move & Party — Imitações (Unity + Kinect v1)

Jogo local de **2 a 4 jogadores**, controlado **100% por gestos** com um **Kinect do Xbox 360 (v1)**.
Em cada rodada um jogador (o **Mestre**) cria um movimento sobre um tema sorteado; os outros
tentam imitá-lo de memória e recebem uma nota de 0 a 100 calculada por similaridade de movimento.
O Mestre ganha 10% da nota de cada imitador.

| Item | Versão |
|---|---|
| Unity | **6000.3.14f1** (changeset `d68c3f99a318`), URP |
| Sensor | Kinect for Xbox 360 (modelo 1414/1473) + adaptador de energia/USB |
| Driver | Kinect for Windows SDK **1.8** |
| Sistema | Windows 10 ou 11, 64 bits |

> Sem Kinect o jogo também roda: ele usa um **sensor simulado** controlado pelo mouse (veja
> [Controles](#controles)).

---

## Sumário

1. [Instalação para humanos (resumo)](#instalação-para-humanos-resumo)
2. [Instalação passo a passo para agentes de IA](#instalação-passo-a-passo-para-agentes-de-ia)
3. [Como jogar](#como-jogar)
4. [Controles](#controles)
5. [Versão final (.exe)](#versão-final-exe)
6. [Solução de problemas do Kinect](#solução-de-problemas-do-kinect)
7. [Estrutura do projeto](#estrutura-do-projeto)
8. [Algoritmo de pontuação](#algoritmo-de-pontuação)

---

## Instalação para humanos (resumo)

1. Instale o **Git**, o **Unity Hub** e, pelo Hub, a Unity **6000.3.14f1**.
2. Instale o **Kinect for Windows SDK 1.8** (e, opcional, o **Developer Toolkit 1.8**).
3. Ligue o Kinect na tomada (adaptador) e numa porta USB **direto no PC**, sem hub.
4. Teste o sensor no **Skeleton Basics-WPF** (Developer Toolkit). Se não aparecer o esqueleto,
   veja [Solução de problemas](#solução-de-problemas-do-kinect) antes de abrir a Unity.
5. Clone este repositório, abra a pasta no Unity Hub (**Add ▸ Add project from disk**) e
   abra a cena `Assets/_Imitacoes/Scenes/Main.unity`.
6. Dê **Play**.

---

## Instalação passo a passo para agentes de IA

Esta seção foi escrita para ser **executada por um agente de IA** com acesso a um terminal
PowerShell no Windows. Regras para o agente:

- Execute os passos **em ordem**. Para cada passo: rode **Verificar**; se passar, pule para o
  próximo; se falhar, rode **Executar** e depois **Verificar** de novo.
- Passos marcados `humano: sim` exigem uma ação física ou uma decisão do usuário. **Pare, explique
  ao usuário o que fazer (texto do campo `instrucao_humano`) e espere a confirmação.**
- Passos marcados `admin: sim` precisam de PowerShell como administrador (o Windows mostra o UAC;
  o usuário precisa aprovar).
- Passos marcados `opcional: sim` podem ser pulados sem quebrar o projeto.
- `$Repo` é a pasta onde este README está. Defina antes de começar:
  ```powershell
  $Repo = (Resolve-Path .).Path   # rode a partir da raiz do repositório
  ```
- Nunca altere configurações de segurança do Windows por conta própria (passo `memory-integrity`):
  isso é decisão do usuário.

### Manifesto (resumo legível por máquina)

```yaml
projeto: Move-Party / Imitações
plataforma: windows-x64
shell: powershell
passos:
  - id: git
    verificar: "git --version"
    executar: "winget install --id Git.Git -e --accept-package-agreements --accept-source-agreements"
  - id: unity-hub
    verificar: "Test-Path 'C:\\Program Files\\Unity Hub\\Unity Hub.exe'"
    executar: "winget install --id Unity.UnityHub -e --accept-package-agreements --accept-source-agreements"
  - id: unity-login
    humano: sim
    instrucao_humano: "Abra o Unity Hub, faça login com sua conta Unity e ative a licença Personal (Preferences > Licenses > Add > Get a free personal license)."
  - id: unity-editor
    verificar: "Test-Path 'C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.14f1\\Editor\\Unity.exe'"
    executar: "& 'C:\\Program Files\\Unity Hub\\Unity Hub.exe' -- --headless install --version 6000.3.14f1 --changeset d68c3f99a318"
  - id: ide
    opcional: sim
    verificar: "Test-Path 'C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\Common7\\IDE\\devenv.exe'"
    executar: "winget install --id Microsoft.VisualStudio.2022.Community -e --override '--add Microsoft.VisualStudio.Workload.ManagedGame --passive --norestart'"
  - id: kinect-sdk
    admin: sim
    verificar: "Test-Path \"$env:windir\\System32\\Kinect10.dll\""
    executar: "ver seção kinect-sdk"
  - id: kinect-toolkit
    opcional: sim
    admin: sim
    verificar: "Test-Path \"$env:KINECT_TOOLKIT_DIR\""
    executar: "ver seção kinect-toolkit"
  - id: kinect-service
    admin: sim
    verificar: "(Get-Service -DisplayName 'Kinect Management').Status -eq 'Running'"
    executar: "ver seção kinect-service"
  - id: kinect-hardware
    humano: sim
    verificar: "Get-PnpDevice -FriendlyName '*Kinect*' -PresentOnly"
    instrucao_humano: "Ligue o adaptador do Kinect na tomada e o USB direto no PC (sem hub/extensão). A luz verde do sensor deve acender."
  - id: memory-integrity
    humano: sim
    verificar: "ver seção memory-integrity"
    instrucao_humano: "Se o Kinect não funcionar, desative a Integridade de Memória (Segurança do Windows > Segurança do dispositivo > Isolamento de núcleo) e reinicie o PC."
  - id: kinect-test
    humano: sim
    instrucao_humano: "Abra o Developer Toolkit Browser v1.8 > Skeleton Basics-WPF > Run, fique a 2–3 m do sensor e confirme se o esqueleto aparece."
  - id: open-project
    verificar: "Test-Path \"$Repo\\Library\""
    executar: "ver seção open-project"
  - id: run-tests
    verificar: "ver seção run-tests"
    executar: "ver seção run-tests"
  - id: play
    humano: sim
    instrucao_humano: "Abra a cena Assets/_Imitacoes/Scenes/Main.unity e clique em Play."
```

### Passo `git` — Git

- **Verificar:** `git --version` retorna uma versão.
- **Executar:**
  ```powershell
  winget install --id Git.Git -e --accept-package-agreements --accept-source-agreements
  ```
- Feche e reabra o terminal depois de instalar (atualiza o `PATH`).

### Passo `unity-hub` — Unity Hub

- **Verificar:** `Test-Path 'C:\Program Files\Unity Hub\Unity Hub.exe'` retorna `True`.
- **Executar:**
  ```powershell
  winget install --id Unity.UnityHub -e --accept-package-agreements --accept-source-agreements
  ```

### Passo `unity-login` — conta e licença Unity (`humano: sim`)

O agente não consegue fazer login. Peça ao usuário:

> Abra o Unity Hub, entre com sua conta Unity e ative a licença gratuita em
> **Preferences ▸ Licenses ▸ Add ▸ Get a free personal license**. Me avise quando terminar.

### Passo `unity-editor` — Unity 6000.3.14f1

- **Verificar:**
  ```powershell
  Test-Path 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe'
  ```
- **Executar** (download de alguns GB, pode levar de 10 a 40 min):
  ```powershell
  & 'C:\Program Files\Unity Hub\Unity Hub.exe' -- --headless install --version 6000.3.14f1 --changeset d68c3f99a318
  ```
- Só o editor Windows é necessário; nenhum módulo extra de plataforma é obrigatório.
- Se o comando falhar, peça ao usuário para instalar pelo Hub: **Installs ▸ Install Editor ▸
  Archive ▸ download archive**, versão **6000.3.14f1**.

### Passo `ide` — editor de código (`opcional: sim`)

Qualquer um serve: Visual Studio 2022, Rider ou VS Code. Para Visual Studio com o suporte a Unity:

```powershell
winget install --id Microsoft.VisualStudio.2022.Community -e --override "--add Microsoft.VisualStudio.Workload.ManagedGame --passive --norestart"
```

### Passo `kinect-sdk` — Kinect for Windows SDK 1.8 (`admin: sim`)

Instala o driver do sensor e a `Kinect10.dll` que o jogo chama por P/Invoke.

- **Verificar:**
  ```powershell
  Test-Path "$env:windir\System32\Kinect10.dll"
  ```
- **Executar** (PowerShell como administrador):
  ```powershell
  $url  = 'https://download.microsoft.com/download/e/1/d/e1dec243-0389-4a23-87bf-f47de869fc1a/KinectSDK-v1.8-Setup.exe'
  $file = "$env:TEMP\KinectSDK-v1.8-Setup.exe"
  Invoke-WebRequest -Uri $url -OutFile $file          # ~222 MB
  Start-Process -FilePath $file -ArgumentList '/quiet','/norestart' -Wait
  ```
- Se a instalação silenciosa não terminar, rode `Start-Process $file` (sem argumentos) e peça ao
  usuário para concluir o assistente.
- Página oficial: <https://www.microsoft.com/en-us/download/details.aspx?id=40278>
- **Importante:** instale o SDK **antes** de abrir o projeto. O editor liga o símbolo de
  compilação `KINECT_V1` automaticamente quando encontra a `Kinect10.dll`; sem ela o jogo roda
  só com o sensor simulado.

### Passo `kinect-toolkit` — Developer Toolkit 1.8 (`opcional: sim`, `admin: sim`)

Traz o **Skeleton Basics-WPF**, usado para testar o sensor fora da Unity.

```powershell
$url  = 'https://download.microsoft.com/download/d/0/6/d061a21c-3af3-4571-8560-4010e96f0bc8/KinectDeveloperToolkit-v1.8.0-Setup.exe'
$file = "$env:TEMP\KinectDeveloperToolkit-v1.8.0-Setup.exe"
Invoke-WebRequest -Uri $url -OutFile $file          # ~384 MB
Start-Process -FilePath $file -ArgumentList '/quiet','/norestart' -Wait
```

- **Verificar:** existe um atalho **Developer Toolkit Browser v1.8** no menu Iniciar, ou
  `Test-Path "$env:KINECT_TOOLKIT_DIR"` retorna `True`.
- Página oficial: <https://www.microsoft.com/en-us/download/details.aspx?id=40276>

### Passo `kinect-service` — serviço Kinect Management (`admin: sim`)

- **Verificar:**
  ```powershell
  Get-Service -DisplayName 'Kinect Management' | Select-Object Status, StartType
  ```
  Esperado: `Running` e `Automatic`.
- **Executar** (administrador):
  ```powershell
  $svc = Get-Service -DisplayName 'Kinect Management'
  Set-Service -Name $svc.Name -StartupType Automatic
  Start-Service -Name $svc.Name
  ```

### Passo `kinect-hardware` — ligar o sensor (`humano: sim`)

Peça ao usuário:

> Ligue o adaptador do Kinect **na tomada** e o USB **direto numa porta do PC** (sem hub nem
> extensão). A luz **verde** do sensor deve acender.

- **Verificar** (o agente pode rodar):
  ```powershell
  Get-PnpDevice -FriendlyName '*Kinect*' -PresentOnly | Format-Table Status, FriendlyName
  ```
  Esperado: 4 dispositivos com `Status = OK`: *Kinect for Windows Audio Array Control*,
  *Camera*, *Device* e *Security Control*. Se a *Camera* aparecer com `Error`, vá para
  `memory-integrity`.

### Passo `memory-integrity` — Integridade de Memória (`humano: sim`)

No Windows 10/11, o recurso de segurança **Integridade de Memória** bloqueia o driver de câmera do
Kinect v1. Sintoma: luz verde acesa, mas o SDK retorna `0x83010007` ou *"No ready Kinect found!"*.

- **Verificar** (somente leitura):
  ```powershell
  (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' -ErrorAction SilentlyContinue).Enabled
  ```
  `1` = ligada (provável causa do problema), `0` ou vazio = desligada.
- **O agente NÃO deve alterar isso sozinho.** Explique ao usuário e peça que ele mesmo decida:

> Para o Kinect v1 funcionar, desative **Segurança do Windows ▸ Segurança do dispositivo ▸
> Detalhes do isolamento de núcleo ▸ Integridade de memória** e reinicie o PC. Isso reduz uma
> proteção contra drivers maliciosos; você pode reativar quando não for usar o Kinect.

### Passo `kinect-test` — testar o sensor fora da Unity (`humano: sim`)

> Abra **Developer Toolkit Browser v1.8 ▸ Samples: C# ▸ Skeleton Basics-WPF ▸ Run**, fique de
> corpo inteiro a 2–3 m do sensor e confirme se o esqueleto verde aparece. **Feche o programa
> depois** (o Kinect v1 só aceita um programa por vez).

Se o esqueleto não aparecer, siga [Solução de problemas](#solução-de-problemas-do-kinect).

### Passo `open-project` — abrir o projeto pela primeira vez

A primeira abertura gera a pasta `Library` (pode levar 5–15 min).

```powershell
$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe'
& $Unity -batchmode -quit -projectPath $Repo -logFile "$Repo\Logs\first-import.log"
```

- **Verificar:** `Test-Path "$Repo\Library"` retorna `True` e o log não contém `error CS`:
  ```powershell
  Select-String -Path "$Repo\Logs\first-import.log" -Pattern 'error CS' | Select-Object -First 10
  ```
- Ao abrir, o script `ProjectSetup` configura o projeto sozinho (Player Settings, assets de
  dados e o símbolo `KINECT_V1`).

### Passo `run-tests` — testes automáticos

Valida o algoritmo de pontuação (8 testes EditMode). O projeto **não** pode estar aberto em outra
janela da Unity.

```powershell
& $Unity -batchmode -projectPath $Repo -runTests -testPlatform EditMode `
  -testResults "$Repo\Logs\test-results.xml" -logFile "$Repo\Logs\tests.log"
```

- **Verificar:**
  ```powershell
  ([xml](Get-Content "$Repo\Logs\test-results.xml")).'test-run'.result   # esperado: Passed
  ```

### Passo `play` — jogar (`humano: sim`)

> Abra o projeto no Unity Hub, abra a cena **`Assets/_Imitacoes/Scenes/Main.unity`** e clique em
> **Play**. No canto superior esquerdo deve aparecer `Fonte: Kinect v1 (SDK 1.8) (Running)`.

---

## Como jogar

1. **Tela inicial:** levante uma mão acima da cabeça.
2. **Quantos jogadores?** Pare a mão sobre um botão (2, 3 ou 4) até o anel completar.
3. **Como jogar:** cartões com as regras; levante a mão para começar (não aparece no "Jogar novamente").
4. **Sorteio do tema** (ex.: *Imite um robô*).
5. **Mestre:** posicione-se, **levante a mão** para começar, espere a contagem e crie o movimento (5 s).
   A música acelera conforme o tempo acaba e um alarme marca o fim.
6. **Replay:** todos assistem ao movimento do Mestre.
7. **Imitadores:** um por vez, em roda a partir do jogador seguinte ao Mestre, levantam a mão e
   repetem o movimento **de memória** (o do Mestre não aparece). A nota sobe na tela, com confete
   a partir de 80.
8. **Resultado da rodada** e, ao final (uma rodada por jogador), **placar final**.

**Pausa:** cruze os braços na frente do peito por ~1 s (fora das gravações). Opções: *Continuar*,
*Pular jogador* (na tela de posicionamento, para quem saiu da sala) e *Voltar ao menu*.

Cores fixas: Jogador 1 vermelho, 2 verde, 3 amarelo, 4 azul.

Dicas para o sensor: fique a **2–3 m**, de corpo inteiro (cabeça aos pés), sem mesa na frente
das pernas, sem sol ou espelho atrás. O Kinect deve ficar entre 0,6 e 1,8 m do chão.

## Controles

| Situação | Ação |
|---|---|
| Com Kinect | Mão levantada move o cursor (só aparece quando há botões na tela); parar sobre o botão seleciona |
| Pausa | Braços cruzados no peito por ~1 s · teclado: **P** (e **Esc** na versão final) |
| Sem Kinect (simulado) | Mouse = mão direita; **Espaço** = as duas mãos para cima; **H** = sair do quadro |
| Debug (só no Editor) | **Enter** confirma/pula · **Esc** volta ao menu · **F1** mostra/esconde info · **F2** troca a fonte (Auto → Kinect → Simulado) · **2/3/4** na seleção de jogadores |

Menu **Imitações** no editor:

- **Configurar Projeto (reaplicar tudo):** refaz as configurações do projeto.
- **Play sempre na cena principal** (ligado por padrão): o Play começa pela `Main.unity` mesmo com outra cena aberta, e num clone novo a `Main.unity` é aberta sozinha no lugar da `SampleScene`.
- **Kinect ▸ Verificar instalação:** mostra se a `Kinect10.dll` foi encontrada.
- **Versão Final ▸ Gerar versão final (.exe):** gera o jogo em `Builds/Imitacoes/` (veja abaixo).
- **Criar Cena Principal:** recria a cena do zero (bonecos 3D); existe também a versão com bonecos 2D.
  Isso descarta ajustes feitos à mão na cena.

## Versão final (.exe)

1. Abra o projeto na Unity e use **Imitações ▸ Versão Final ▸ Gerar versão final (.exe)**.
   O jogo sai em `Builds/Imitacoes/Imitacoes.exe`: Windows 64 bits, tela cheia, sem teclas nem
   texto de debug e sem a seta do mouse.
2. Para apresentar em outro PC, copie a pasta `Builds/Imitacoes` **inteira** e instale lá o
   [Kinect for Windows Runtime 1.8](https://www.microsoft.com/en-us/download/details.aspx?id=40277)
   (ou o SDK 1.8). Não precisa da Unity.
3. Para sair do jogo: **Alt+F4**. **Esc** abre a pausa.
4. Se algo der errado no PC da apresentação, o log fica em
   `%USERPROFILE%\AppData\LocalLow\Move & Party\Imitações\Player.log`.

**Versão Final ▸ Gerar versão de teste (com debug)** gera a mesma coisa com as teclas de debug
ligadas (Enter, F1, F2…), útil para ensaiar.

## Solução de problemas do Kinect

| Sintoma / código | Significado | O que fazer |
|---|---|---|
| `0x83010014` *Kinect não encontrado* | Sensor não conectado ou sem energia | Confira o adaptador na tomada e o USB |
| `0x83010007` (`E_NUI_INSUFFICIENTBANDWIDTH`) | O SDK vê o sensor, mas não consegue abrir a câmera de profundidade | 1) Integridade de Memória (passo `memory-integrity`); 2) serviço Kinect Management (passo `kinect-service`); 3) desconecte webcam, HD externo e outros USB e desabilite a webcam interna no Gerenciador de Dispositivos; 4) sem hub/extensão |
| *No ready Kinect found!* no Skeleton Basics | Mesmo problema acima, ou a Unity está com o sensor aberto | Feche a Unity e siga a linha acima |
| `Kinect10.dll não encontrada` | SDK não instalado | Passo `kinect-sdk`, depois reabra a Unity |
| Boneco pisca ou some | Jogador perto demais, pernas escondidas ou pouca luz | Fique a 2–3 m, de corpo inteiro; o aviso *"Afaste-se um pouco do sensor"* indica distância curta |
| Cursor da mão tremendo | Ruído natural do Kinect v1 | Ajuste em `[UI] ▸ Hand Cursor`: diminua *Cursor Filter ▸ Min Cutoff* (mais firme) ou aumente *Beta* (mais rápido) |
| Funciona no Skeleton Basics mas não no jogo | O Skeleton Basics ainda está aberto | Feche-o: o Kinect v1 só aceita um programa por vez |

Controladoras USB problemáticas com o Kinect v1: ASMedia, Fresco Logic, VIA e AMD anteriores ao
Ryzen. O programa gratuito **USBTreeView** mostra qual controladora atende cada porta.

Referências: [K2VR – E_NUI_INSUFFICIENTBANDWIDTH](https://docs.k2vr.tech/en/360/troubleshooting/insufficientbandwidth/),
[K2VR – conflitos de USB](https://docs.k2vr.tech/en/360/troubleshooting/notgenuine/).

## Estrutura do projeto

```
Assets/_Imitacoes/
├── Scenes/Main.unity                 cena principal
├── Data/                             ScriptableObjects
│   ├── Players/Jogador1..4.asset     nome e cor de cada jogador
│   ├── Themes/ThemeCatalog.asset     lista de temas sorteáveis (edite no Inspector)
│   └── Settings/GameSettings.asset   tempos, gravação, gestos, pontuação do Mestre
├── Scripts/
│   ├── Runtime/  [Imitacoes.Runtime]  não depende do Kinect
│   │   ├── Body/         modelo de corpo (BodyFrame, JointData, IBodySource)
│   │   ├── Sources/      MockBodySource (simulado), ActiveBodySelector (jogador ativo + filtro)
│   │   ├── Motion/       MotionClip, MotionRecorder, filtro One Euro
│   │   ├── Scoring/      MotionComparer (DTW), PoseFeatures
│   │   ├── Interaction/  HandCursor, GestureButton, GestureDetector
│   │   ├── Game/         GameManager, máquina de estados, MatchState
│   │   ├── UI/  Visual/  telas, bonecos 3D (Avatar3DView) e 2D (SkeletonView)
│   │   └── Config/       GameSettings, PlayerConfig, ThemeCatalog
│   ├── Kinect/   [Imitacoes.Kinect]   KinectBodySource: P/Invoke na Kinect10.dll (só com KINECT_V1)
│   └── Editor/   [Imitacoes.Editor]   ProjectSetup, SceneBuilder
└── Tests/EditMode/                    testes do algoritmo de pontuação
Docs/KINECT_SETUP.md                   anotações antigas de instalação (inclui Kinect v2)
```

```
Kinect10.dll (System32) ←P/Invoke— Imitacoes.Kinect → Imitacoes.Runtime ← Imitacoes.Editor / Tests
```

## Algoritmo de pontuação

1. Para cada frame: direção de cada osso, com o corpo girado para ficar de frente. Assim a nota
   não depende de posição, altura ou para onde a pessoa está virada.
2. Distância entre frames: ângulo médio entre os ossos, com peso maior para braços e pernas e
   ênfase nos ossos que o Mestre mais movimentou; o giro do corpo também conta.
3. **DTW** (alinhamento temporal) tolera começar um pouco antes ou depois; a versão espelhada
   também é aceita.
4. A distância vira nota de 0 a 100 por uma curva exigente.
5. O Mestre recebe **10% da soma** das notas dos imitadores (configurável em `GameSettings`).
