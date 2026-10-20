# Rascunho limitado da alternativa Speech

`TempleSpeechProfile` valida MAP, ator, porta inicial, vínculo/INT, metadados e
GAM conhecidos. `TempleSpeechDraft` aceita somente a sequência verificada de
cinco escolhas de inteligência efetiva >=4, com Speech tagged. Não altera o
perfil neutro do personagem; é um ensaio explícito INT 5/tag ativo.

Percurso: Node001 → Node002 → Node002a/Node003 → Node014 → Node015 → Node015a.
Node002a grava local 7=1. A confirmação acrescenta locais 12=1/13=2, variável
do mapa 20=2 e global 10=2. As cinco escritas são publicadas numa transação
somente no banco **destacado**, depois de todas as escolhas.

Cancelamento, escolha fora da ordem, ramo não mapeado, token/revisão antigos e
estado inicial alterado recusam o avanço/commit. Referências ao local 16 seguem
indisponíveis. O modelo não executa reação, rolagem/RNG original, transferência
de inventário, movimento do NPC, porta, XP ou avanço do mundo ativo.
O teste da tag usa a condição OR comprovada; não simula uma rolagem oculta.

Quatro grupos adicionais cobrem prefixo, ausência de mutação antes de confirmar,
uma revisão, repetição, inteligência/tag, cancelamento, reset/load, estado
desconhecido e snapshots independentes. **266 grupos passaram**. O relatório
`--temple-state-report` repete o percurso nos dados Epic reais e confere que a
variável original do MAP permanece zero.

Próximo: arquivo de estado desse ensaio, separado do checkpoint do mundo, que
aceite apenas os estados inicial/aceito reconhecidos. GPT-6.1 Sol, esforço Alto.
