# Comparação das listas do patch e do master

O relatório do templo compara `scripts/scripts.lst` somente nos arquivos DAT já
descobertos por `GameData.SourcePriority`. Não procura outra instalação, muda
a prioridade ou usa um script alternativo no mundo. Não extrai arquivos.

| Fonte | Tamanho da lista | Locais de actemvil | Locais de aitemdor |
| --- | --- | --- | --- |
| patch000.dat, lista resolvida | 104534 | 16 | 9 |
| master.dat | 104532 | 16 | 9 |

Ambas mantêm os programas nos índices 748/749. O INT `actemvil` resolvido está
no master; o patch examinado não contém esse recurso. O hash do INT permanece
`D865DF15AA53995F69D9A211034622E34A5D9D9AC10E406AEBBCE6E3B19D61F5`.
A lista master tem hash `D9B40481C920119E67538F4129C5148B531255CF776DD9BAA88835828F5C8525`.

Logo, a referência ao índice local 16 não é explicada por uma diferença dessas
contagens entre patch e master, nem deve ser "corrigida" escolhendo outra lista.

No CE, `scriptGetLocalVar`/`scriptSetLocalVar` somam offset da instância e índice.
`mapGetLocalVar`/`mapSetLocalVar` verificam o limite do vetor de locais do mapa,
sem aplicar o limite individual do script nessas chamadas. O efeito concreto
da referência depende também da ordem de alocação e do ramo executado. Não foi
provado crash, alias com outra instância ou impacto no ramo Speech.

Relatório reproduzível: `--temple-dialogue-report`, evidência
`.local/temple-script-source-variants.json`. Os hashes, tamanhos, contagens e
presença do programa em cada arquivo são registrados sem texto/bytes originais.
O adaptador do NPC continua desabilitado; coleta/devolução estáticas funcionam.

Próximo marco do NPC: representar separadamente o estado suportado e recusar
referências fora de seu contrato, sem herdar implicitamente armazenamento de
outras instâncias. GPT-6.1 Sol, esforço Alto.
