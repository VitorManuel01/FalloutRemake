# Revisão da identificação visual — 19/10/2026

Revisão externa sobre `1dded47`, conferida e incorporada sobre `b63297d` na branch
`codex/scene-review-fixes`. Escopo: apresentação, seleção, painéis e verificações.

- Os sete recipientes compartilham o hex com o bloqueador técnico de PID
  `0x02000043`, FID `0x02000015`. Nesses casos a placa vira um contorno baixo,
  deixando caixa, tampa e indicador de conteúdo visíveis. O corpo original,
  sua chave, colisão de seleção e bloqueio de navegação continuam presentes.
- A inspeção e o modo caminhar usam a mesma resolução de sobreposição. O clique
  prioriza o recipiente sobre seu bloqueador técnico, ou a luz sobre o obstáculo
  do mesmo hex. “Próximo” permite consultar cada objeto original, informa a posição
  no conjunto e fica desabilitado sem seleção ou sem alternativas.
- Correção de um detalhe da revisão: somente a luz do hex 26499 compartilha um
  bloqueador técnico. Nas luzes 19071 e 19457 da elevação 2, o outro objeto tem
  PID `0x02000643`, FID `0x02000650`; esses obstáculos comuns recebem uma base
  vermelha sob a luminária. Não foram tratados como proxies invisíveis.
- Emitir luz e bloquear passagem são propriedades independentes. A luz do
  offset 432688 mantém a luminária, com base vermelha e descrição do bloqueio.
- Filtros e descrição têm rolagem independente; legenda e ações continuam visíveis.
  O título ocupa até duas linhas, com texto completo na dica. A navegação respeita
  a largura disponível. O cabeçalho permite rolagem horizontal quando necessário.
  Janelas gráficas usam pixels reais, sem reduzir todo o painel de 900px.
- Os três diálogos limitam seu tamanho à janela e têm conteúdo rolável. Reconstruir
  o mundo fecha também o inventário. Carregar preserva o modo e, quando ainda
  disponível na elevação carregada, a seleção pela chave fonte.
- Rótulos de recipientes, itens posicionados e passagem aberta mantêm escala
  equivalente a 14px, contorno escuro e distância vertical do personagem. Rótulos
  distantes são omitidos no enquadramento geral; sua área estimada não pode invadir
  o HUD ou outra legenda. Não é um sistema geral de oclusão de textos por paredes.
- Tampas abrem pela dobradiça traseira. Recipientes especiais permanecem fechados,
  inclusive quando vazios. Caches e referências visuais são limpos no encerramento.

## Validação

`dotnet build "Fallout Remake.csproj"`; `dotnet run --project tools/TempleInspector -- --self-test`;
`tools/Validate-Temple.ps1` com os dados locais. A bateria completa inclui o novo
marcador `SCENE_REVIEW_OK`, além das verificações anteriores de importação,
navegação, inventário, checkpoint v4 e passagem aplicada.

O teste de leitura visual agora salva o recipiente **vazio**, reinicia para o
estado cheio e carrega novamente, verificando texto, material, tampa e indicador.
Depois devolve o item e verifica o fechamento. O avanço usa a conclusão normal
da caminhada, exigindo Salvar habilitado, Parar desabilitado e rota apagada.

O teste adicional cobre sete recipientes e três luzes em três ângulos: 60 raios
com paredes filtradas, para verificar especificamente alvo e obstáculo do mesmo
hex. Paredes reais continuam interceptando cliques normalmente. Também verifica
navegação pelos bloqueadores originais, alternância de seleção, título longo,
limites dos painéis e diálogos, zoom 8–220 e fechamento de inventário.

Capturas e limites reais de janela são conferidos com o runner isolado em
1440×900, 1280×720, 1366×768 e 1024×768. Exemplo reproduzível:

```powershell
.\tools\Run-QuietGodot.ps1 -GodotExe <exe-local> -ProjectPath C:\Dev\FalloutRemake `
  -CapturePath C:\Dev\FalloutRemake\.local\revisao.png -Check SceneReview -Width 1280 -Height 720
```

O runner registra o desktop de entrada antes/depois, saída, erro e encerramento.
O modo sem imagem usa o viewport lógico configurado; não substitui a matriz de
resoluções gráficas. Nenhum editor ou jogo é aberto no desktop ativo. Os testes
usam somente slots locais de teste. Arquivos proprietários continuam fora do Git;
a reconstrução dos bytes dos mapas permanece verificada.

Resultado final: compilação sem avisos/erros, **225 grupos sintéticos**, **13/13
etapas da bateria completa** e bateria gráfica completa em 1440×900, com saída
de erro vazia. As outras três resoluções passaram pelo teste visual específico.
Todos os runners registraram o mesmo desktop de entrada antes/depois.
Manifesto: `.local/temple-validation-20261020-000210-37edb803/manifest.json`.

A primeira bateria sem imagem falhou por uma recusa de acesso durante gravações
consecutivas das preferências de teste. Sem alterar a persistência, a repetição
completa e a execução gráfica passaram. A causa dessa ocorrência transitória não
foi estabelecida; os logs foram preservados. Recusas deliberadas de arquivo
bloqueado e JSON incompleto também fazem parte dos testes de preferências.

## Próximo incremento

Validar a interação de ativação do explosivo já recolhido, usando as propostas e
checagens existentes, sem ampliar para combate geral. Modelo recomendado:
GPT-6.1 Sol, esforço Alto. Antes, incorporar eventual nova observação visual do usuário.
