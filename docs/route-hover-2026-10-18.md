# Décimo nono marco — prévia do percurso

Branch `codex/temple-route-preview`, base `53c2e14`.

No modo Caminhar, apontar para o chão mostra o percurso azul e a quantidade de
passos antes do clique. O destino é verde quando alcançável, vermelho se bloqueado
ou desconectado e âmbar para uma saída alcançável. Apenas clicar solicita movimento.
O caminho usa a mesma árvore de alcance do personagem, sem refazer BFS a cada frame.

A prévia funciona com a grade oculta. Some durante caminhada, arrasto da câmera,
consulta de inventário, inspeção de outra elevação ou quando o ponteiro está sobre
os painéis/fora do mapa. A mensagem temporária preserva o estado anterior do painel.
Apontar para uma porta apenas indica que o clique pode consultá-la/aproximar-se;
a prévia exata de aproximação fica para o próximo incremento.

Projeção do chão compartilhada com o clique; mesma malha de trajeto para prévia
e movimento, com materiais e alturas separados. As cores são guias da prévia,
sem alterar regras, portas, XP, rolagens ou bytes fonte. Saídas continuam terminais
e a passagem depende de Enter e da validação ao chegar.

Validação: compilação sem avisos/erros; Godot gráfico e sem janela com saída 0 e
`ROUTE_HOVER_OK`, log gráfico de erros vazio. Caso real de 23 passos: projeção da
tela, prévia e caminho clicado iguais; bloqueio, destino desconectado, saída com
12 passos, fora do mapa, painéis, movimento e elevações. Confere reuso do alcance,
posição/portas/perícia intactas e reconstrução byte a byte. Teste usa o mesmo
manipulador com coordenadas de tela, sem mover o cursor do sistema. Captura
1440×900 `.local/temple-hover.route-preview.png` revisada; logs `.local/hover-*.log`.

Os 109 grupos sintéticos anteriores cobrem as regras reutilizadas; não foram
criadas regras de movimento novas. Próximo: prévia de aproximação às portas.
Modelo: GPT-6.1 Sol, esforço Alto (High).
