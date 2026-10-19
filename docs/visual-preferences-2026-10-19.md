# Vigésimo quarto marco — preferências visuais locais

19/10/2026 · branch `codex/visual-preferences`, base `be0e436`.

G / Grade, O / Piso original e V / Auto salvam a escolha imediatamente, pelas
teclas ou pelos botões. A abertura seguinte restaura os controles e a cena antes
de começar a exploração. Reiniciar, trocar de camada e carregar checkpoint não
gravam nem substituem as escolhas.

Arquivo ignorado pelo Git: `.local/visual-preferences.json`, esquema versão 1,
com três booleanos. Separado de `preview-settings.json` (pasta do jogo) e do
checkpoint (estado do mundo). Não contém arte, mapas, IDs ou dados do personagem.
Os padrões continuam: grade ligada, piso original desligado e Auto ligado.

Sem arquivo, usa os padrões e não cria nada até a primeira escolha explícita.
Arquivo inválido/incompatível ou leitura indisponível usa os padrões, informa o
problema e preserva o arquivo. Uma escolha posterior pode substituí-lo. Gravação
usa temporário no mesmo diretório e substituição ao final. Em caso de falha, a
opção permanece nesta sessão, com aviso; uma gravação posterior tenta novamente.
O piso original continua dependente dos recursos locais: se indisponível, usa
volumes nesta sessão e conserva a preferência solicitada para a próxima abertura.

Validações:

- Compilação com zero avisos/erros; **122 grupos sintéticos aprovados**. Quatro
  novos grupos cobrem ausência de arquivo, todas as combinações, formato inválido,
  versão/tipos/campos/tamanho e preservação do arquivo anterior em falha real de
  substituição por bloqueio do Windows, com limpeza do temporário.
- Integração completa sem janela e no desktop isolado: `VISUAL_PREFERENCES_OK`;
  teclas/botões, restauração, controles, visibilidade, reset/camadas/checkpoint sem
  gravação de preferências, falha de gravação sem reverter a opção da sessão,
  arquivo inválido e reparo explícito. Portas, navegação, ator e bytes do MAP íntegros.
- Nova instância do Godot, com e sem renderização:
  `VISUAL_PREFERENCES_STARTUP_OK`. Restaurou grade desligada, piso original ligado
  e Auto desligado antes de carregar o mapa; arquivo permaneceu intacto.
- Gráfico completo: saída 0 em ~19,4 s; reabertura: saída 0 em ~2,9 s. Ambos
  com stderr vazio e desktop de entrada `Default` antes/depois. Captura de
  reabertura 1440 × 900 revisada.

Testes usam somente `.local/visual-preferences-test.json`, sem ler ou criar as
preferências reais. `--preview-test` prepara o arquivo com opções diferentes dos
padrões; depois `--visual-preferences-smoke` verifica a abertura num novo processo.
Para renderizar este segundo teste sem janela no desktop ativo, o runner aceita
`-Check VisualPreferencesStartup`. Não executar os dois testes simultaneamente,
pois compartilham o arquivo de teste.

Logs `.local/preferences-{build,synthetic,preview}.log`,
`.local/preferences-startup-headless.log`, `.local/temple-preferences.stdout.log`,
`.local/temple-preferences-reopened.stdout.log` e resultados JSON correspondentes.
Captura `.local/temple-preferences-reopened.preferences-startup.png`, fora do Git.

Próximo sugerido: preparar a primeira coleta de item do templo, preservando IDs
e quantidades. Modelo recomendado: GPT-6.1 Sol, esforço Alto (High).
