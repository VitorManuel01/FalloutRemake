# Décimo sexto marco — nomes e descrições originais

Branch `codex/temple-object-names`, base `12f0a7e`.

Selecionar um objeto exibe seu nome e descrição originais, junto dos IDs e estado
do inspetor. Os textos são lidos em memória; o projeto não distribui esses arquivos.
O idioma é descoberto pelos caminhos disponíveis: exige exatamente um conjunto
completo de `pro_item`, `pro_crit`, `pro_scen`, `pro_wall`, `pro_tile`, `pro_misc`.
Aqui, o conjunto encontrado foi `text/english/game/`. Não traduz nem mistura idiomas.

O nome usa o `messageId` do PRO (palavra no offset 4); a descrição usa esse ID +1.
Não calcula o ID textual pelo PID. MSG é lido por trios `{id}{áudio}{texto}`, com
IDs assinados, Latin-1 para o perfil legado, campos vazios, remoção de quebras de
linha e substituição de IDs duplicados conforme CE. Limites: 1 MiB, 1023 caracteres
por campo e 20000 registros. Sem interpretação de diálogos, áudio, filtros ou macros.
O nome no título é encurtado para 80 caracteres; o texto original não é regravado.

Mensagem ausente/vazia ou perfil de idioma indisponível mantém o título por tipo
e ID. Arquivo inválido ou ausente desativa somente os textos daquele tipo. As
dependências ficam em um catálogo separado, sem mudar hashes ou versão do checkpoint.

Validação: **106 grupos sintéticos**, seis novos para MSG, corrupção e limites,
duplicatas, IDs do PRO, idiomas, ordem dos tipos e fingerprints. Compilação sem
avisos/erros; Godot gráfico e sem janela com saída 0 e `OBJECT_TEXT_OK`. Leu seis
listas com **6804 entradas** e resolveu **340 ocorrências de protótipos com nome**
nos três MAPs. Conferiu título, descrição, PID, fallback, posição intacta e fontes
reconstruídas byte a byte. Log gráfico de erros vazio; captura 1440×900
`.local/temple-names.names.png` revisada. Logs `.local/names-*.log`.

Limites: nomes que scripts alteram durante o jogo não são simulados. O nome de
protótipo de uma criatura pode diferir do personagem nomeado por um script.

Referência CE local `e97087b9582f37075db347a89898887320753f8b`: `proto.cc`
(`protoGetMessage`, `protoGetName`, `protoGetDescription`, carregamento `pro_%.4s`),
`proto.h` e `message.cc` (`_message_load_field`, `_message_add`).

Próximo: consulta da árvore de inventário original, sem transferência de itens.
Modelo: GPT-6.1 Sol, esforço Alto (High).
