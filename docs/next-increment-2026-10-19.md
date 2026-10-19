# Retomada após o marco 49

Estado: `codex/explosive-use-proposal`, prévia compilando e bateria unificada
aprovada em dez etapas, com 167 grupos sintéticos. Nenhuma alteração publicada.
Ícones I/J/B, orientação entre áreas e proteção da caminhada estão na prévia.
A barreira continua bloqueada; temporizador e proposta não ativam itens.

## Próximo incremento pequeno: propriedade de uma unidade usada

Separar a quantidade recolhida da quantidade ainda carregada, sem devolver ao
recipiente uma unidade já posicionada/consumida. Preservar mapa, chave do
proprietário e chave do registro fonte; IDs numéricos não são exclusivos.
A conservação deve ser: quantidade original = restante no recipiente + carregada
+ posicionada + consumida. Não substituir o PID do objeto importado no MAP.

Antes de aplicar uma proposta, verificar novamente identidade, disponibilidade,
quantidade um, posição e prazo. Cancelamento não altera nada. Falhas de validação
não podem deixar uma unidade retirada sem seu evento ou objeto correspondente.
Não habilitar o botão de uso enquanto essa operação não for atômica e persistível.

O checkpoint precisa representar os novos estados e rejeitar duplicações,
quantidades inventadas e referências externas. Manter leitura de v1/v2 com
inventário/uso neutros; uma mudança de schema deve ser explícita e testada.
A destruição da barreira e o cenário de substituição virão em incremento separado,
com hashes do PRO adicional, overlay de navegação e identidade de objeto criado.

## Validação mínima

Construir fixtures sintéticas para pilha parcial, cancelamento, origem obsoleta,
dupla aplicação, retorno de unidade já usada e restauração incompatível. Conferir
conservação antes/depois do checkpoint e dados originais byte a byte. Só depois
ligar relógio/eventos e projetar dano/segurança para criaturas. O relatório dos
marcos 42–45 registra a fronteira de acesso e as questões do CE fixado.

Usar `tools/Validate-Temple.ps1` com caminhos explícitos já documentados. Capturas
apenas por `Run-QuietGodot.ps1`; não abrir editor/jogo no desktop ativo.
Não adicionar assets proprietários ao Git nem modificar instâncias do usuário.

Modelo recomendado: GPT-6.1 Sol, esforço Alto.
