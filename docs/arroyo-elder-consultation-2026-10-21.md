# Consulta dos textos da anciã

Depois de **Ir até a anciã**, selecione-a e use **Anciã · conversa em consulta**.
A janela lê o MSG original da instalação local. A chegada verificada, a
elevação 0, a identidade original selecionada e um hex vizinho são exigidos.
Durante a caminhada ou outra janela, a consulta permanece bloqueada.

Trecho disponível, com lista de mensagens original 11:

- Node001/103: opção 104→Node006; 106→Node008.
- Node006/127: 129→Node006a→Node008; 130→Node013.
- Node008/135: opção 136→Node999/fim.
- Node013/152: opção 153→Node999/fim.

As opções 105/128/154 que levam a Node015 aparecem desabilitadas. Esse ramo
transfere dinheiro, cria/adiciona item e escreve progresso no original; não
é executado pela consulta. A ponte Node006a é percorrida somente para obter
a continuação textual. Reação, locais e globais de diálogo não são aplicados,
nem gravados no checkpoint. A consulta pode ser fechada ou reiniciada.

Os hashes do MAP, INT e scripts.lst, a declaração de 10 locais, os offsets,
procedimentos-alvo, inteligência mínima, reação declarada nas opções e IDs
de resposta são validados. O MSG deve pertencer à mesma língua/ator e conter
todos os IDs necessários. O Git contém somente código e IDs; nenhum texto
original ou asset foi copiado para os fontes/documentos.

Validação: builds sem avisos/erros, **329 grupos sintéticos**, auditoria real
`ARROYO_ELDER_AUDIT_OK`, cenário headless e captura isolada com
`ARROYO_ELDER_CONSULTATION_OK`. O teste percorre os dois caminhos, recusa
opções indisponíveis e callbacks antigos, inclusive um botão de antes do
reinício com o mesmo offset, fecha/reabre e carrega a partida com a janela
aberta. O checkpoint completo permanece idêntico: XP, itens, globais,
progresso do templo, portas e posição.

Evidências locais: `.local/arroyo-elder-consultation-synthetic.log`,
`.local/arroyo-elder-consultation-audit.json`,
`.local/arroyo-elder-consultation-headless.log`,
`.local/arroyo-elder-consultation-isolated.json` e
`.local/arroyo-elder-consultation-1280.arroyo-elder-consultation.png`.
Captura isolada aprovada em 41,6 s, com desktop `Default` preservado.

Próximo marco: auditar os atributos originais necessários à reação da anciã,
antes de aplicar os efeitos do diálogo à partida. **GPT-6.1 Sol, esforço Alto**.
