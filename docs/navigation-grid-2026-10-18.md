# Décimo oitavo marco — grade de navegação opcional

Branch `codex/temple-navigation-grid`, base `b69e4a6`.

**G / Grade**, no painel de caminhada, mostra ou oculta os hexágonos de alcance,
bloqueio e saída. Caminhada, personagem e traçado do percurso continuam funcionando
com a grade oculta. A escolha permanece ao alternar modo, atualizar portas,
atravessar áreas e reiniciar a prévia. Inicialmente ligada; não é persistida em disco.

Pode ser combinada com **O / Piso original** para visualizar os pisos durante a
caminhada. O controle não interfere no filtro Piso, nas fontes nem no checkpoint.
O painel inferior ganhou espaço para o novo controle sem sobrepor o rodapé.

Validação: compilação sem avisos/erros; testes Godot gráfico e sem janela com
`NAVIGATION_GRID_OK`, saída 0 e log gráfico de erros vazio. Confere G e botão,
modo de caminhada, percurso de 23 passos, alcance, grade após reconstrução,
ida/retorno do exterior com portas e XP, reinício, piso original e reconstrução
byte a byte. Há verificação de que os controles cabem acima do rodapé.
Os **109 grupos sintéticos** anteriores permanecem aprovados; não houve mudança
nas regras do importador ou da navegação. Captura 1440×900
`.local/temple-grid.grid.png` revisada; logs `.local/grid-*.log`.

Próximo marco recomendado: prévia do percurso e número de passos antes do clique,
sem mover o ator ao apenas apontar. Modelo: GPT-6.1 Sol, esforço Alto (High).
