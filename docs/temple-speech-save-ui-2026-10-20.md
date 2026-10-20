# Salvar e carregar o ensaio pela janela

**Salvar resultado** fica disponível somente depois das cinco escolhas aceitas.
O arquivo `.local/checkpoints/temple-speech.json` contém apenas o perfil e as
variáveis do ensaio; não substitui o checkpoint da partida. **Carregar ensaio**
substitui explicitamente as escolhas atuais pelo estado validado do arquivo.
Abrir a janela não carrega automaticamente o resultado anterior.

Durante escolhas incompletas, salvar é recusado também no callback, preservando
o resultado anterior. Um arquivo inválido ou inacessível mantém o banco, o token,
a posição da conversa e a partida. Uma carga válida cancela o rascunho anterior
e cria um banco com token novo. Callbacks de janelas fechadas/recriadas não
podem salvar nem carregar na janela atual.

O ensaio não contém o texto MSG, nem imagens, scripts ou bytes originais do MAP.
Não concede XP, não move o NPC e não habilita combate ou progresso de missão.

Validação: build sem avisos/erros e `TEMPLE_SPEECH_SAVE_UI_OK` na integração
headless. Exercita botões reais, arquivo próprio com GUID, bloqueio de sobrescrita
parcial, rejeição de JSON inválido, carga válida, reabertura, callbacks antigos e
igualdade integral do checkpoint ativo. O arquivo de teste é removido no final;
os arquivos pessoais de ensaio/checkpoint não são usados pelo fixture.
Captura isolada em 1024×768 também passou (`.local/speech-save-isolated.json`),
com os botões legíveis, logs limpos e desktop de entrada `Default` preservado.

Próximo: orientar o percurso até o NPC pela própria seleção, respeitando os
bloqueios e a elevação. GPT-6.1 Sol, esforço Alto.
