# Vigésimo primeiro marco — destino das saídas na prévia

Branch `codex/temple-exit-preview`, base `bfb77a2`.

Apontar para uma saída mostra nome da área, elevação, acesso pelo percurso e
condição do destino. Um destino interno ou já carregado pode ser validado; outro
MAP do perfil é indicado como **validar ao chegar**, sem carregar essa área ao
apenas apontar. Áreas fora do perfil, recarga não implementada e destinos bloqueados
continuam explícitos. Destino válido e caminho alcançável são informações distintas.

`TempleTravel.InspectExitAt` compartilha a análise estrutural com a passagem real,
aceitando coordenadas de consulta sem mover o ator. Preserva guardas de campos,
FID, sobreposições conflitantes, origem/destino bloqueados e ciclo de recarga.
`WorldSession.PreviewExitAt` consulta apenas o perfil e as áreas já em memória;
não chama o carregador, não inicia adaptadores e não aumenta o conjunto de áreas
do checkpoint. Ao chegar e usar Enter, a passagem real continua revalidando tudo.

O nome vem de `data/maps.txt`, com fallback pelo ID quando ausente. Também aparece
no inspetor da saída e nas mensagens de passagem/chegada. Alcançar uma saída não
atravessa automaticamente. Cor âmbar representa uma saída alcançável, sem prometer
que a área de destino esteja disponível; o texto informa essa condição.

Validação: **114 grupos sintéticos**, cinco novos para consulta remota, carregador
não chamado, cache/áreas do checkpoint, destino indisponível/bloqueado, conflitos e
elevação da origem diferente da do ator. Compilação sem avisos/erros; Godot gráfico
e sem janela com `EXIT_ROUTE_PREVIEW_OK`, saída 0 e log gráfico de erros vazio.
Caso real: saída exterior a 12 passos, saída interna desconectada e saída da vila
fora do perfil. Snapshot do checkpoint antes/depois igual e apenas o MAP inicial
carregado. Vila lida para esse teste, sem alegar simular sua progressão. MAPs
reconstruídos byte a byte. Captura 1440×900
`.local/temple-exit-hover.exit-preview.png` revisada; logs `.local/exit-hover-*.log`.

Próximo recomendado: enquadrar o percurso completo pela câmera, considerando os
painéis da interface. Modelo: GPT-6.1 Sol, esforço Alto (High).
