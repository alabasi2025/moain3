# نماذج مفتاح البروكسي (الوكلاء المتاحون)

المصدر: `GET {OPENAI_BASE_URL}/models` — تاريخ الفحص 2026-11-09. اختُبر كل نموذج بطلب فعلي (`tools/test_proxy_models.py`).

**الإجمالي: 81 نموذجاً — يعمل 75، يفشل 6 (404 من المزوّد).**

## النماذج المطلوبة

| الطلب | الاسم الصحيح في البروكسي | الحالة |
|---|---|---|
| OPS 5.5 | `claude-opus-5-5` | ✅ |
| Fable 5.1 | `claude-fable-5-1` | ✅ |
| GPT Astra 6 | `gpt-6-astra` | ✅ |

> سبب الخطأ السابق: جرّبت أسماء مخمّنة (`opus-5.5`، `fable-5.1`، `gpt-astra-6`) بدل استعلام `/models` أولاً.

## Anthropic Claude (19)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `claude-fable-5` | ✅ | 3.6 |  |
| `claude-fable-5-1` | ✅ | 2.9 |  |
| `claude-haiku-4-5` | ✅ | 1.0 |  |
| `claude-haiku-5-5` | ✅ | 2.7 |  |
| `claude-opus-4-5` | ✅ | 1.1 |  |
| `claude-opus-4-6` | ✅ | 2.0 |  |
| `claude-opus-4-6-1m` | ✅ | 2.7 |  |
| `claude-opus-4-7` | ✅ | 1.0 |  |
| `claude-opus-4-7-extended-cache` | ✅ | 1.1 |  |
| `claude-opus-4-8` | ✅ | 1.1 |  |
| `claude-opus-5` | ✅ | 1.6 |  |
| `claude-opus-5-5` | ✅ | 1.8 |  |
| `claude-opus-5-5-extended-cache` | ✅ | 1.8 |  |
| `claude-opus-5-low` | ✅ | 1.7 |  |
| `claude-sonnet-4-5` | ✅ | 2.2 |  |
| `claude-sonnet-4-6` | ✅ | 1.1 |  |
| `claude-sonnet-4-6-1m` | ✅ | 1.0 |  |
| `claude-sonnet-5` | ✅ | 0.7 |  |
| `claude-sonnet-5-5` | ✅ | 1.0 |  |

## OpenAI GPT (22)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `gpt-5` | ✅ | 1.5 |  |
| `gpt-5-codex` | ❌ | 0.6 | Error code: 404 - Error code: 404 - {'error': {'message': 'litellm.Not |
| `gpt-5-mini` | ✅ | 1.6 |  |
| `gpt-5-nano` | ✅ | 2.1 |  |
| `gpt-5.1` | ✅ | 0.7 |  |
| `gpt-5.2` | ✅ | 0.9 |  |
| `gpt-5.2-codex` | ❌ | 0.5 | Error code: 404 - Error code: 404 - {'error': {'message': 'litellm.Not |
| `gpt-5.3-codex` | ✅ | 1.1 |  |
| `gpt-5.4` | ✅ | 1.0 |  |
| `gpt-5.4-mini` | ✅ | 0.9 |  |
| `gpt-5.4-nano` | ✅ | 1.6 |  |
| `gpt-5.5` | ✅ | 1.4 |  |
| `gpt-5.6-luna` | ✅ | 0.9 |  |
| `gpt-5.6-luna-max` | ✅ | 0.9 |  |
| `gpt-5.6-sol` | ✅ | 1.1 |  |
| `gpt-5.6-terra` | ✅ | 1.1 |  |
| `gpt-6-astra` | ✅ | 1.7 |  |
| `gpt-6-luna` | ✅ | 1.1 |  |
| `gpt-6-luna-low` | ✅ | 0.9 |  |
| `gpt-6-sol` | ✅ | 1.3 |  |
| `gpt-6.1-sol` | ✅ | 1.5 |  |
| `gpt-6.1-terra` | ✅ | 1.3 |  |

## DeepSeek (13)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `deep-seek-v4-flash` | ✅ | 1.4 |  |
| `deep-seek-v4-flash-baseten` | ✅ | 1.1 |  |
| `deep-seek-v4-flash-no-think` | ✅ | 0.7 |  |
| `deep-seek-v4-flash-vision-exp` | ✅ | 0.7 |  |
| `deep-seek-v4-pro` | ✅ | 0.9 |  |
| `deep-seek-v4-pro-baseten` | ✅ | 1.2 |  |
| `deep-seek-v4.1-flash` | ✅ | 0.8 |  |
| `deep-seek-v4.1-flash-low` | ✅ | 0.6 |  |
| `deep-seek-v4.1-flash-max` | ✅ | 0.7 |  |
| `deep-seek-v4.1-flash-no-think` | ✅ | 1.3 |  |
| `deep-seek-v4.1-flash-novita` | ✅ | 0.9 |  |
| `deepseek-v4-pro-0813` | ✅ | 0.9 |  |
| `deepseek-v4-pro-0813-baseten` | ✅ | 1.0 |  |

## Zhipu GLM (6)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `glm-5p2` | ✅ | 1.4 |  |
| `glm-5p3` | ✅ | 4.3 |  |
| `glm-5p3-flash` | ✅ | 1.3 |  |
| `glm-5p3-flash-baseten` | ✅ | 1.1 |  |
| `glm-5p3-flash-low` | ✅ | 0.7 |  |
| `glm-5p3-flash-no-think` | ✅ | 0.6 |  |

## xAI Grok (3)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `grok-4.5` | ✅ | 5.4 | No. I won't comply with a demand to outp |
| `grok-4.6` | ✅ | 3.9 |  |
| `grok-4.7` | ✅ | 5.4 | No. I won't reply with an exact forced p |

## Moonshot Kimi (2)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `kimi-k3` | ✅ | 1.4 |  |
| `kimi-k3.1-pro` | ✅ | 1.8 |  |

## Xiaomi MiMo (3)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `mimo-v2.6-flash` | ✅ | 2.6 |  |
| `mimo-v2.6-flash-no-think` | ✅ | 2.0 |  |
| `mimo-v2.6-pro` | ✅ | 2.5 |  |

## MiniMax (7)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `minimax-m2p5` | ❌ | 0.3 | Error code: 404 - Error code: 404 - {'error': {'message': 'litellm.Not |
| `minimax-m2p7` | ✅ | 0.7 |  |
| `minimax-m3` | ✅ | 0.8 |  |
| `minimax-m3-rft-slide-qwen38-baseten` | ✅ | 2.2 |  |
| `minimax-m3-rft-slide-v1` | ❌ | 0.3 | Error code: 404 - Error code: 404 - {'error': {'message': 'litellm.Not |
| `minimax-m3-rft-slide-v1-qat` | ✅ | 1.2 |  |
| `minimax-m3p1-flash` | ✅ | 2.5 |  |

## NVIDIA Nemotron (2)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `nemotron-3-ultra` | ✅ | 0.7 |  |
| `nemotron-3.5-super-vl` | ✅ | 0.8 |  |

## Arcee Trinity (3)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `trinity-large-preview` | ❌ | 0.3 | Error code: 404 - Error code: 404 - {'error': {'message': "litellm.Not |
| `trinity-large-thinking` | ✅ | 0.7 |  |
| `trinity-mini` | ❌ | 0.4 | Error code: 404 - Error code: 404 - {'error': {'message': "litellm.Not |

## أخرى (1)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `muse-spark-open` | ✅ | 1.1 |  |

## أخرى (1)

| النموذج | الحالة | زمن (ث) | ملاحظة |
|---|---|---|---|
| `muse-spark-open` | ✅ | 1.1 |  |
