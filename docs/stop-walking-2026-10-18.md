# Décimo quarto marco — parar sem reiniciar

Branch `codex/temple-stop-walking`, base `ef35ded`.

**Esc / Parar** interrompe o percurso mantendo portas, XP, posição e câmera.
Antes de iniciar um passo, para no hex atual; durante a interpolação, termina apenas
esse passo e descarta o restante do caminho. Pedidos repetidos são idempotentes.
O botão mostra “Parando…” enquanto termina o passo e fica desabilitado quando
parado. Salvar continua indisponível até alcançar o centro do hex.

Outro destino pode ser solicitado depois. O traçado é reduzido ao passo final e
removido quando o percurso termina, inclusive em chegadas normais. Esc não troca
elevação, não atravessa uma saída automaticamente nem restaura o estado inicial.

Validação: compilação sem erros/avisos, **96 grupos sintéticos**, dois novos
para continuidade, parada repetida, limites entre passos e reinício do percurso.
Godot gráfico e sem janela com `STOP_WALKING_OK`, saída 0 e log gráfico de erros
vazio. A integração confere Esc, botão, parada antes e durante um passo, avanço por
frames, posição discreta e visual, bloqueio de salvamento, acompanhamento, limpeza
do traçado, portas/XP e reconstrução byte a byte da fonte. Captura 1440×900
`.local/temple-stop.stop.png` revisada; logs `.local/stop-*.log`.

Próximo marco recomendado: ocultação opcional de paredes que encobrem o personagem,
sem alterar bloqueios ou dados originais. Modelo: GPT-6.1 Sol, esforço Alto (High).
