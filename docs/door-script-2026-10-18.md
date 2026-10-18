# Sexto marco — análise estática da segunda porta

Branch `codex/temple-script-analysis`, base `1e5b24e`. Dados locais Epic e referência
CE no commit `e97087b9582f37075db347a89898887320753f8b`, sem executar o INT.

## Identidade e diferença entre fonte e sessão

`ARCAVES`, mapa 3, elevação 0: objeto ID 102, hex 11108, offset 137396,
SID 50331648. O SID resolve a entrada **25, base zero**, de `scripts/scripts.lst`:
`doorl100.int`, com **8 variáveis locais**. No MAP, o registro ainda tem offset de
locais `-1` e quantidade 0; isso não significa que o script dispense variáveis.

O programa vem de `master.dat`, tem 12054 bytes e SHA-256
`A8791ECAA9C7CFA3528D537AA5F1E8F20067E4BB51995C22B14DFEFAD02741BA`.
Há 25 entradas de procedimentos e 24 endereços de corpo distintos: as entradas 0 e 1
compartilham o endereço 1632. Não se deve eliminar aliases ou renumerar entradas.

**A porta gravada está destrancada (`openFlags=0`), mas o script a tranca na primeira
entrada no mapa.** O CE obtém o número de locais do LST e os aloca zerados sob demanda.
`map_enter_p_proc` inicializa L3=1, L0=0 e L1=1 quando L3 era zero. Em seguida, tranca
se L0=0 e destranca caso contrário. `map_update_p_proc` repete essa sincronização.

## Condições confirmadas no bytecode

L0: 0 representa trancada, 1 destrancada. L1: 0 representa armadilha ativa, 1 inativa.
L2 registra detecção da armadilha; L3, inicialização; L5 impede repetir a recompensa
de fechadura; L6 participa da recompensa de descoberta/desarme. Essas funções foram
inferidas pelos usos; não são nomes armazenados no INT.

| Procedimento / offset | Condição e resultado observado |
| --- | --- |
| `map_enter_p_proc`, 4872 | Inicializa os locais acima e aplica a tranca conforme L0. |
| `map_update_p_proc`, 5004 | Tranca se L0=0; destranca se L0≠0. |
| `use_p_proc`, 3102 | Consulta Traps (11), modificador 0. Com L1=1, só impede o uso se a porta estiver trancada. Sem override, o CE faz a abertura/fechamento normal. |
| `use_skill_on_p_proc`, 3708 | Lockpick (9): chama `Unlock_Lock` se L0=0; caso contrário, chama `Set_Lock` para retrancar. Traps (11) consulta L1 e encaminha o desarme quando ativa. |
| `Unlock_Lock`, 9918 | Rola Lockpick com modificador **+100**, guarda o resultado e chama `Lockpick_Door`. |
| `Lockpick_Door`, 10066 | Sucesso: L0=1 e `obj_unlock`. Dá **25 XP uma vez**, guardados por L5. Falha crítica: `jam_lock`. Falha comum: mensagem, sem destrancar. |
| `Set_Lock`, 10708 | Lockpick +100 e `Lock_Door`: sucesso volta L0 a 0 e tranca; a recompensa usa o mesmo L5. |
| `use_obj_on_p_proc`, 4002 | O ramo constante `0==0` é sempre escolhido; somente o PID 84 aciona as rotinas de ferramenta, com modificador **+120**. |
| `damage_p_proc`, 4436 | O ramo de material escolhido por `0==0` destrói a porta quando o metarule 49 retorna tipo explosão (6). Outros tipos seguem uma mensagem. |

O bônus **+100 não é chance garantida de 100%**: o CE chama
`randomRoll(skillGetValue + modifier, criticalChance, ...)`, com suas regras de
dificuldade, críticos e eventual substituição por membro do grupo. Esta etapa não
reproduz esse cálculo nem promete uma probabilidade final.

Os ramos de PID 77, pé de cabra (PID 20) e explosivos (PIDs 51/85) permanecem fisicamente
no programa, mas estão depois do `else` do teste constante verdadeiro. Sua presença
não comprova que possam ser acionados por uso de item nesta variante. As rotinas
auxiliares de armadilha e `Pry_Door` também não devem ser habilitadas apenas pelo nome.

Se uma armadilha estiver ativa (L1=0), o uso pode detectá-la e impedir a ação; falhar
na detecção chama `Damage_Critter`, que solicita dano aleatório 10–20, tipo explosão
com bypass de armadura, e depois faz L1=1. Isso é um caminho latente; a inicialização
confirmada desta porta começa com L1=1. Não foi implementado dano no remake.

No CE, `_obj_use_skill_on` recusa fechaduras emperradas **antes** de chamar o script.
Após o script, sem override, chama `skillUse`; o caso Lockpick desse fallback não
faz uma segunda rolagem. A ordem evita interpretar incorretamente a falha crítica
como um estado que pode ser ignorado na tentativa seguinte.

## Incremento e validação

`IntProgram` lê o perfil clássico observado: bootstrap de 42 bytes, quantidade e
registros de 24 bytes, nomes e limites de instruções. Usa o salto absoluto do bootstrap
para encontrar o código. Não presume tamanho da área de strings: preserva essa área
opaca. Endereços de corpos/condições precisam coincidir com instruções, e pushes
preservam os 32 bits do operando, inclusive float/string, sem interpretá-los.

O relatório inclui origem/hash, índices e todos os seis campos de cada entrada.
Opcodes desconhecidos dentro do intervalo CE permanecem numéricos. Um trecho físico
de procedimento não é prova de alcançabilidade: aliases e código morto são preservados.
Perfis de bootstrap diferentes são recusados explicitamente. Não é um decompilador,
VM, validador completo de fluxo/pilha ou leitor completo da heap de strings.

- **44 grupos sintéticos** aprovados: 7 novos, cobrindo aliases, cópias independentes,
  limites, corrupção, truncamento, operandos tipados e procedimentos importados.
- Dados reais: 25 entradas, 2930 instruções incluindo bootstrap/inicialização;
  offsets de identificadores 646, strings 1480 e início do código 1488.
- Compilação Godot .NET sem erros/avisos. Relatório completo e disassembly de apoio
  permanecem privados em `.local/`; nenhum programa extraído entrou no Git.
- Regressão Godot sem janela: prévia, navegação e primeira porta aprovadas, saída 0.
  Caminho de recurso com travessia recusado pelo CLI, saída 1.

Comando reproduzível, a partir da raiz:

```powershell
dotnet run --project tools/TempleInspector -- --data-dir 'D:\Games\fallout2\Fallout 2 English' --script scripts/doorl100.int
```

Acrescente `--instructions` para os offsets, opcodes e operandos brutos por trecho.
Os números acima e as condições foram conferidos no programa real e no código CE;
os testes sintéticos validam o leitor, não substituem uma comparação jogável com o original.

## Próximo incremento

O [adaptador restrito foi implementado](door-adapter-2026-10-18.md) no sétimo marco.
A [integração na prévia com perfil de teste](lockpick-preview-2026-10-18.md) foi entregue
no oitavo. A comparação jogável segue pendente; abaixo fica o plano histórico.

Implementar um adaptador restrito a esta identidade/hash, com oito locais da sessão,
inicialização e tranca, e uma ação explícita de Lockpick. Separar a resolução da
rolagem das alterações de estado para testar sucesso, falha, falha crítica e XP único.
Antes de ativá-la na prévia, definir os atributos/perícias mínimos do personagem e
conferir a sequência no jogo original. Portas desconhecidas continuam dependendo de script.

## Referência

[Fallout 2 CE, commit consultado](https://github.com/alexbatalov/fallout2-ce/tree/e97087b9582f37075db347a89898887320753f8b):
`interpreter.h/.cc` (layout, pushes, `opIf` e chamadas), `interpreter_extra.cc`
(opcodes e ordem de argumentos), `scripts.cc` (LST, locais e eventos), `map.cc`
(alocação zerada), `proto_instance.cc` (uso, trancas e emperramento), `skill.cc`,
`skill_defs.h`, `random.h` e `proto_types.h` (rolagens e enums).
