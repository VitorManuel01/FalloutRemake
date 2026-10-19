# Vigésimo marco — prévia de aproximação à porta

Branch `codex/temple-door-route-preview`, base `da8bc5d`.

No modo Caminhar, apontar para uma porta mostra o nome original e o percurso até
o lado alcançável mais próximo. A prévia e o clique compartilham os mesmos critérios:
vizinhos caminháveis, fora de saídas, menor distância e desempate pelo número do
hex. Quando já está ao alcance, mostra a orientação da interação sem propor andar.

Porta trancada com adaptador conhecido pode ter aproximação; isso não destranca,
rola perícia ou concede XP. Portas sem caminho ou que exigem scripts não suportados
mostram o impedimento. Clique seleciona/aproxima; U e L continuam ações explícitas.
Apontar não seleciona o objeto nem altera a sessão.

Resultado reutilizado enquanto a porta apontada e a navegação não mudam. Caminhar,
trocar alvo, abrir/fechar ou tentar arrombar invalidam a prévia. Voltar da porta ao
chão restaura a mensagem de percurso comum, inclusive no mesmo hex de aproximação.

Validação: compilação sem avisos/erros; Godot gráfico e sem janela com saída 0 e
`DOOR_ROUTE_PREVIEW_OK`, log gráfico de erros vazio. Teste com picking da tela,
mesma aproximação e início/fim após clique, orientação ao alcance, porta trancada,
cache invalidado, troca para o chão, seleção/ator/perícia/XP e bytes preservados.
As verificações anteriores de portas e 109 grupos sintéticos permanecem aplicáveis.
Captura 1440×900 `.local/temple-door-hover.door-route.png` revisada.

Próximo: identificar destino e disponibilidade das saídas na prévia sem carregar
áreas ao apenas apontar. Modelo: GPT-6.1 Sol, esforço Alto (High).
