# Fallout Remake — prévia 3D do templo

Projeto pessoal em Godot 4 .NET/C#, começando por Fallout 2. A direção visual futura é
um ambiente isométrico 3D inspirado em Wasteland 3. A cena principal já permite
explorar o Temple of Trials com formas provisórias, inspecionar os objetos originais
e mover um marcador por hexágonos válidos na elevação do personagem. A primeira porta pode ser
aberta e fechada; a segunda já aceita arrombamento com um perfil de teste,
atualizando os caminhos sem alterar os dados originais.
O leitor verifica a reconstrução dos bytes do mapa antes de montar a cena.
O personagem ainda tem apenas as regras mínimas deste teste, sem combate ou arte final.

**Correção do primeiro marco:** o interior é `maps/arcaves.map`, ID 3.
`maps/artemple.map`, ID 126, é a área externa com Klint. O inspetor agora usa o interior
como padrão. O cadastro local e os scripts dos objetos confirmaram essa distinção.

## Executar

Abra `project.godot` no **Godot .NET** e pressione **F5**. A prévia lê a pasta
salva em `.local/preview-settings.json` (configurada nesta máquina). Em outra máquina,
use **Abrir dados** e selecione a pasta de idioma que contém `master.dat` e `critter.dat`.
A escolha fica salva apenas localmente; não é necessário extrair ou exportar o MAP.

- **WASD / botão direito:** mover a câmera; **roda:** zoom; **Q/E:** girar.
- **F / Entrada:** reenquadrar a vista da entrada sem mover o personagem; **Home / Ver tudo:** enquadrar a camada.
- **Clique:** consultar objeto ou piso; painel lateral: elevações 0–2 e filtros.
  O inspetor inclui nome e descrição originais quando disponíveis na instalação.
- **Próximo no mesmo hex:** alternar objetos sobrepostos sem confundir seus IDs.
- **I / Inventário:** consultar o conteúdo original do objeto selecionado.
  Exibe hierarquia, quantidades e descrições; não transfere itens. A janela
  suspende o avanço da caminhada enquanto está aberta.
- **J / Recolher:** abre o recipiente selecionado com as quantidades restantes
  nesta sessão. Use **Aproximar-se**, aguarde chegar e abra novamente para retirar
  uma unidade ou a pilha. A bolsa é salva com K e restaurada com R; reiniciar
  a prévia esvazia a bolsa e restaura os recipientes.
- **B / Bolsa:** consulta os itens recolhidos, nomes, quantidades e área de origem,
  inclusive depois de viajar e carregar checkpoint. B também fecha a janela.
  **Devolver 1 / Devolver esta pilha** repõem o item no recipiente original
  quando o personagem estiver ao alcance. A devolução também é salva com K.
  **Voltar ao recipiente** solicita a aproximação à origem na área atual.
  Em outra área visitada, **Ir à passagem** guia o primeiro trecho; use Enter
  e abra B novamente para continuar. Depois de chegar, confirme a devolução.
  As janelas J/B mostram peso por pilha e carga atual. O perfil usa **Força 5 /
  capacidade 150 lb**. Retiradas que ultrapassam a carga são recusadas; devolver
  libera espaço. Itens com peso ainda indisponível não recebem estimativa.
  Pilhas maiores que uma unidade também permitem escolher **Qtd.** para retirar
  ou devolver um número específico, com revalidação no clique.
- **M / Caminhar:** alternar para caminhada na elevação do personagem; clique no chão para mover
  o marcador azul. Pontos verdes indicam alcance, vermelhos bloqueios e âmbar saídas.
- **Portas:** no modo Caminhar, clique na primeira porta para se aproximar; depois
  use **U / Abrir porta**. Clique além dela para atravessar. U também permite fechar.
- **L / Arrombar:** após se aproximar da segunda porta, tenta destrancar. U abre a
  passagem depois. L muda para **Retrancar** quando destrancada; os 25 XP não repetem.
- **Reiniciar prévia:** voltar à entrada e restaurar as portas. Trocar de elevação
  no painel apenas inspeciona; o personagem e os XP permanecem no lugar.
  Durante uma caminhada, termine ou use Esc antes de mudar a elevação exibida.
- **Enter / Passar:** ao chegar a uma saída interna, atravessa para a elevação de
  destino, preservando portas e XP. Caminhe na elevação onde o personagem está.
- **K / Salvar:** checkpoint local quando estiver parado. **R / Carregar:**
  recupera posição, portas e XP das áreas carregadas. Reiniciar não apaga esse slot.
  Os atalhos usam letras para evitar conflito com os controles do editor Godot.
  O schema atual é v4; checkpoints v1/v2/v3 continuam carregando. Itens posicionados,
  consumidos e substituições de cenário têm estado próprio; a ativação ainda não
  possui ação na interface.
- **C / Personagem:** reenquadra e acompanha o personagem. WASD ou arrasto retomam
  a câmera livre; recentrar mantém o percurso em andamento.
- **O / Piso original:** compara o piso com imagens lidas da instalação local.
  O filtro Piso funciona nos dois modos. Texturas ficam somente em memória.
- **Esc / Parar:** interrompe a caminhada ao concluir o passo atual, preservando
  portas e XP. Depois, pode escolher outro destino sem reiniciar a prévia.
- **V / Auto**, junto de Paredes: oculta paredes que encobrem o personagem.
  A opção é visual; os bloqueios e a seleção dos objetos permanecem.
- **G / Grade:** mostra ou oculta os hexágonos de navegação. Caminhada e traçado
  continuam ativos sem a grade; combine com O para ver o piso original.
- **T / Ver percurso:** enquadra o trajeto apontado ou em andamento no espaço
  livre dos painéis. Também inclui a porta de destino quando houver aproximação.
  A caminhada continua; **C** retoma o acompanhamento do personagem.
- **Preferências visuais:** as escolhas de **G**, **O** e **V**, por tecla ou botão,
  são salvas automaticamente nesta máquina e restauradas ao abrir a prévia.
  Reiniciar a prévia e carregar um checkpoint mantêm essas escolhas.
- **Apontar para o chão no modo Caminhar:** mostra o percurso e os passos antes
  do clique. A prévia não move o personagem e também funciona sem a grade.
  Ao apontar para uma porta, mostra a aproximação alcançável ou sua condição.
  Saídas mostram destino e disponibilidade. Apontar não carrega outra área;
  a passagem continua sendo validada ao chegar e usar Enter.
  Recipientes também mostram a aproximação; clique seleciona e caminha até eles,
  e **J** permite recolher depois de chegar. **T** inclui o recipiente na vista.

Paredes são volumes de referência nos hexes originais. Alturas, dimensões e cores
são provisórias. O bloqueio usa FID/flags dos objetos, sem depender do piso ou dos
filtros visuais. Portas fechadas bloqueiam; saídas encerram o percurso e aguardam
Enter. Passagens internas e saídas entre templo, exterior e vila usam os destinos
originais; outras áreas continuam indisponíveis. O retorno preserva portas e XP.
A interação exige proximidade e marcador parado. Apenas o script conferido
da segunda porta possui adaptador; uma passagem ocupada não pode ser fechada.
O perfil de teste usa PER/AGI/SOR 5, Arrombamento 20, dificuldade normal e primeiro
dia. Com o bônus +100 desta porta, a tentativa tem sucesso em todas as faces do d100.

Na raiz do repositório, com SDK .NET capaz de compilar `net8.0` e runtime .NET 8:

```powershell
dotnet build 'Fallout Remake.csproj'
dotnet run --project tools/TempleInspector -- --self-test
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English'
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --export-local
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --navigation

dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --script scripts/doorl100.int

dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --door-adapter-test
```

O último caminho foi confirmado no manifesto local da Epic em 16/10/2026. Em outra
máquina, informe explicitamente a pasta de idioma que contém `master.dat` e `critter.dat`.
Não selecione a pasta `Fallout1in2`. Para conferir a área externa, acrescente
`--map maps/artemple.map`.

Para testar a prévia e o leitor dentro do Godot, após compilar:

```powershell
$godotExe = 'D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
& $godotExe --headless --path . -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --preview-test
& $godotExe --headless --path . res://scenes/ImportSmoke.tscn -- --data-dir 'D:\Games\fallout2\Fallout 2 English'
```

Sucesso: `PREVIEW_TEST_OK`, `NAVIGATION_TEST_OK`, `DOOR_TEST_OK` e
`LOCKPICK_PREVIEW_OK`, `TRAVEL_PREVIEW_OK`, `WORLD_PREVIEW_OK` e
`CHECKPOINT_PREVIEW_OK`, `CHARACTER_CAMERA_OK`, `FLOOR_ART_PREVIEW_OK`,
`STOP_WALKING_OK`, `WALL_OCCLUSION_OK`, `OBJECT_TEXT_OK`,
`INVENTORY_INSPECTOR_OK`, `NAVIGATION_GRID_OK`, `ROUTE_HOVER_OK`,
`DOOR_ROUTE_PREVIEW_OK`, `EXIT_ROUTE_PREVIEW_OK`, `ROUTE_CAMERA_OK`,
`ITEM_CHECKPOINT_OK`, `CONTAINER_LOOT_OK`, `PLAYER_BAG_OK`, `ITEM_RETURN_OK`,
`CONTAINER_ROUTE_OK`, `BAG_ORIGIN_OK`, `CARRY_WEIGHT_UI_OK`, `QUANTITY_TRANSFER_OK`, `SCRIPTED_CONTAINER_OK`,
`INVENTORY_ART_UI_OK`,
`ELEVATION_WALK_GUARD_OK`,
`VISUAL_PREFERENCES_OK` / `IMPORT_SMOKE_OK`,
seguidos de saída com código 0.
O caminho do Godot foi obtido do atalho local; ajuste-o em outra máquina.
Durante o uso simultâneo do computador, faça capturas pelo
`tools/Run-QuietGodot.ps1`, com caminhos explícitos. Esse runner usa um desktop
isolado do Windows; `--headless` continua sendo a opção sem renderização.
Após `--preview-test`, o teste de reabertura pode ser executado sem janela com
`--visual-preferences-smoke` após `--`. Ele lê apenas o arquivo de preferências
de teste e deve emitir `VISUAL_PREFERENCES_STARTUP_OK`. Para conferir com imagem,
o runner aceita `-Check VisualPreferencesStartup`. Ambos preservam as opções reais.
Informe `-CapturePath "$PWD\.local\temple-preview.png"` ao runner isolado para
capturar a cena. Ele já executa `--preview-test`, que também gera
uma captura `.selection.png` da porta selecionada, `.navigation.png` do percurso
e `.door.png` da primeira passagem aberta, além de `.lockpick.png` da segunda
porta após arrombar e atravessar. `.route-frame.png` mostra o percurso enquadrado.
`.exit.png` mostra a passagem interna disponível e
`.travel.png` a chegada na elevação 1; `.outside.png` mostra o exterior. Todas ficam fora do Git.

O leitor abre os dados em modo de leitura e imprime um resumo JSON, com contagens,
origens, hashes e divergências dos dados originais. Sempre verifica uma reconstrução
byte a byte a partir dos campos lidos. O modo `--export-local` grava a representação
intermediária em `.local/arcaves.v2.json`, incluindo os bytes de origem e protótipos;
esse arquivo é privado, regenerável e ignorado pelo Git. Execute esse modo na raiz.
Não inclua dados originais, exportações ou arte convertida no repositório.

O modo `--script` inspeciona a estrutura de um INT sem executá-lo. Acrescente
`--instructions` para incluir opcodes e operandos brutos. Os trechos são físicos,
sem prova automática de fluxo ou comportamento. A análise da segunda porta confirmou
que o script a tranca na entrada, embora o MAP grave o estado destrancado. Sua
interação já usa um perfil mínimo de teste na prévia.

O modo `--door-adapter-test` já valida a inicialização, tranca e arrombamento da
segunda porta com resultados determinísticos. Sucesso destranca e concede 25 XP uma
vez; falha crítica emperra e impede nova tentativa. A sessão mantém os dados fonte
intactos. O comando emite `TEMPLE_ADAPTER_TEST_OK`; não representa uma rolagem jogável.

- [Formatos, limites e próximo incremento](docs/temple-import.md)
- [Coordenadas e limites da prévia 3D](docs/preview-3d.md)
- [Navegação: regras e limites](docs/navigation.md)
- [Portas: comportamento e validação atual](docs/doors-2026-10-17.md)
- [Segunda porta: condições do script e leitor INT](docs/door-script-2026-10-18.md)
- [Adaptador de arrombamento: regras e validação](docs/door-adapter-2026-10-18.md)
- [Arrombamento na prévia: perfil e controles](docs/lockpick-preview-2026-10-18.md)
- [Passagens internas: destinos e estado do personagem](docs/travel-2026-10-18.md)
- [Áreas externas e retorno: cache e validação](docs/world-2026-10-18.md)
- [Checkpoint local: formato e restauração](docs/checkpoint-2026-10-18.md)
- [Câmera no personagem: controles e validação](docs/camera-2026-10-18.md)
- [Piso original: formato e referência visual](docs/floor-art-2026-10-18.md)
- [Parada da caminhada: continuidade e controles](docs/stop-walking-2026-10-18.md)
- [Revelar o personagem: paredes e limites](docs/wall-occlusion-2026-10-18.md)
- [Nomes e descrições: mensagens e idioma](docs/object-text-2026-10-18.md)
- [Inventário original: consulta e limites](docs/inventory-inspector-2026-10-18.md)
- [Grade de navegação: visualização opcional](docs/navigation-grid-2026-10-18.md)
- [Prévia do percurso: estados e controles](docs/route-hover-2026-10-18.md)
- [Prévia da porta: aproximação e estado](docs/door-route-hover-2026-10-18.md)
- [Prévia da saída: destino e carregamento](docs/exit-route-hover-2026-10-18.md)
- [Validação isolada: executar sem abrir janelas no desktop ativo](docs/quiet-validation-2026-10-18.md)
- [Enquadrar percurso: câmera, espaço livre e validação](docs/route-camera-2026-10-19.md)
- [Preferências visuais: persistência local e reabertura](docs/visual-preferences-2026-10-19.md)
- [Fontes dos itens: transferência e conservação de quantidades](docs/item-transfer-domain-2026-10-19.md)
- [Itens no mundo: checkpoint v2 e compatibilidade com v1](docs/item-checkpoint-2026-10-19.md)
- [Coleta: aproximação, quantidades restantes e janela](docs/container-loot-2026-10-19.md)
- [Bolsa do personagem: consulta e origem dos itens](docs/player-bag-2026-10-19.md)
- [Devolver itens: origem, proximidade e conservação](docs/item-return-2026-10-19.md)
- [Recipientes no modo Caminhar: prévia, clique e enquadramento](docs/container-route-2026-10-19.md)
- [Bolsa: solicitar aproximação ao recipiente de origem](docs/bag-origin-2026-10-19.md)
- [Peso base original: layout PRO e validações](docs/item-base-weight-2026-10-19.md)
- [Carga do perfil de teste: limite, pesos e compatibilidade](docs/carry-profile-2026-10-19.md)
- [Pesos na interface: pilhas, carga e estados indisponíveis](docs/carry-weight-ui-2026-10-19.md)
- [Quantidade escolhida: edição, carga e transferência](docs/item-quantity-2026-10-19.md)
- [Baú do templo: referências externas e limites do adaptador](docs/temple-chest-script-2026-10-19.md)
- [Recipiente vazio: informar scripts e impedimentos](docs/container-status-2026-10-19.md)
- [Rota de retorno: áreas visitadas e passagens explícitas](docs/item-origin-route-2026-10-19.md)
- [Bolsa: orientação de retorno entre áreas](docs/bag-return-route-2026-10-19.md)
- [Arte do inventário: FID original, FRM e paleta](docs/inventory-art-2026-10-19.md)
- [Ícones em J/B: apresentação e fallback](docs/inventory-icon-ui-2026-10-19.md)
- [Coleta nas elevações: percurso validado e barreira pendente](docs/temple-collection-2026-10-19.md)
- [Barreira explosiva: condição e objeto de substituição](docs/temple-barrier-2026-10-19.md)
- [Distância em hexes e referência de alcance da barreira](docs/hex-distance-2026-10-19.md)
- [Explosivo: temporizador confirmado e questões da referência](docs/explosive-profile-2026-10-19.md)
- [Consulta I: ícones e inventário original preservado](docs/original-inventory-icons-2026-10-19.md)
- [Inspeção de elevação: preservar o passo e o percurso](docs/elevation-walk-guard-2026-10-19.md)
- [Bateria local sem janela: um comando e manifesto de resultados](docs/validation-battery-2026-10-19.md)
- [Explosivo: proposta validada sem ativar ou consumir](docs/explosive-use-proposal-2026-10-19.md)
- [Retomada: propriedade, conservação e checkpoint do próximo incremento](docs/next-increment-2026-10-19.md)
- [Propriedade de itens: carregados, posicionados e consumidos](docs/item-use-ownership-2026-10-19.md)
- [Checkpoint v3: conservação, áreas e compatibilidade](docs/item-use-checkpoint-2026-10-19.md)
- [Commit de uso: revisão, sessão e revalidação](docs/explosive-use-commit-2026-10-19.md)
- [Ticks pendentes: prazo, restauração e conclusão explícita](docs/pending-use-clock-2026-10-19.md)
- [Ordem dos vencimentos: parar no primeiro evento e preservar a sobra](docs/use-deadline-ordering-2026-10-19.md)
- [Itens posicionados: camada visual, identidade e tempo lógico](docs/placed-use-preview-2026-10-19.md)
- [Efeito da barreira: alvo, perfis e proposta sem mutação](docs/barrier-effect-proposal-2026-10-19.md)
- [Cenário substituído: identidade e projeção isolada da passagem](docs/barrier-scene-projection-2026-10-19.md)
- [Ensaio 3D: coleta real, posição, prazo e barreira preservada](docs/temple-explosive-preview-audit-2026-10-19.md)
- [Continuação projetada: chegada e recipientes da elevação 2](docs/projected-temple-continuation-2026-10-19.md)
- [Retomada: contrato e aceitação do próximo commit de barreira](docs/next-barrier-commit-2026-10-19.md)
- [Entidades de teste: obstáculos, luzes e recipientes com estados visuais](docs/entity-readability-2026-10-19.md)
- [Checkpoint v4: substituição de cenário e consumo na mesma operação](docs/barrier-checkpoint-v4-2026-10-19.md)
- [Efeito aplicado: ida à elevação 2, coleta e retorno com checkpoint](docs/applied-temple-continuation-2026-10-19.md)
- [Validação do quarto marco: caminhada por hexágonos](docs/validation-navigation-2026-10-17.md)
- [Validação do terceiro marco: prévia 3D](docs/validation-preview-2026-10-17.md)
- [Validação do segundo marco: scripts, objetos e inventários](docs/validation-objects-2026-10-17.md)
- [Validação histórica do primeiro marco](docs/validation-2026-10-16.md)
