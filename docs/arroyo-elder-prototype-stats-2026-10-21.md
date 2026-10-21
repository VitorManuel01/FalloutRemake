# Dados originais de atributos para auditar a reação

`CritterPrototypeData.Read` acrescenta uma visão imutável dos campos
serializados de PRO de criaturas: cabeçalho específico, 35 atributos base,
35 bônus, 18 perícias e cauda de corpo/XP/tipo. A cópia exige PID, tamanho e
fingerprint coerentes e preserva valores originais, inclusive negativos.
Não calcula atributos efetivos, deriva estatísticas ou executa combate.

A ordem **no arquivo** foi conferida no Community Engine, revisão
`e97087b9582f37075db347a89898887320753f8b`:

| Campo | Offset, bytes |
| --- | --- |
| headFid / pacote de IA / equipe | 32 / 36 / 40 |
| flags dos dados da criatura | 44 |
| 35 bases / 35 bônus / 18 perícias | 48 / 188 / 328 |
| corpo / experiência / tipo de morte | 400 / 404 / 408 |
| tipo de dano natural opcional | 412 |

Arquivos de 412 bytes conservam o último campo como ausente (`null`); não
inventam bytes. O CE usa dano normal como fallback em execução, regra distinta
da preservação da origem. Arrays devolvidos não expõem o armazenamento interno;
os pseudoatributos atuais, índices >=35, não existem nesse bloco serializado.

A anciã usa `proto/critters/00000013.pro`, 416 bytes, SHA256
`782FEACF76511BBE415789C4BF8DB9F01C8B126122BC7BF4E7937E1AE3DB3876`.
Atributos SPECIAL base: **4/5/5/5/5/5/5**; bônus **0/0/0/0/0/0/0**.
Carisma, índice 3, portanto tem base **5**, bônus **0**. Esses números
não são uma afirmação de atributo efetivo após modificadores.

`--arroyo-elder-report` agora inclui esses dados e sua origem. O relatório
genérico `--script ... --instructions` inclui também o trecho físico entre
o início do código e o primeiro procedimento, permitindo inspecionar a
inicialização do programa sem executar INT ou confundir esses temporários
com os locais declarados em scripts.lst.

Validação: **332 grupos sintéticos**, compilação sem avisos/erros, relatório
real `ARROYO_ELDER_AUDIT_OK`, fontes/MAP preservados. Evidências locais:
`.local/arroyo-elder-stats-synthetic.log`, `.local/arroyo-elder-stats-audit.json`
e `.local/arroyo-elder-with-initialization.json`.

Fontes primárias: [proto.cc](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/proto.cc),
[critter.cc](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/critter.cc),
[stat_defs.h](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/stat_defs.h),
[stat.cc](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/stat.cc).

Próximo marco: projeção separada do preâmbulo de reação para um perfil neutro
explicitamente definido, conferindo condições, inicialização do programa e
modificadores antes de qualquer conversa ativa. **GPT-6.1 Sol, esforço Alto**.
