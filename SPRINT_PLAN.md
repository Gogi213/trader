# План оставшихся спринтов

## 📦 SPRINT 3: Динамическое обновление ордеров (КРИТИЧНО)
**Время:** 4-6 часов
**Приоритет:** ВЫСОКИЙ

### Задачи:
1. **Переписать TradingStrategyService.cs:**
   - Добавить PositionManager через DI
   - Заменить примитивные поля на OrderState
   - Использовать Position для хранения информации о позиции

2. **Реализовать динамическое обновление ордеров:**
   ```csharp
   // Псевдокод логики
   OnOrderBookUpdate(orderBook) {
       var bestBid = orderBook.Bids.First().Price;
       var bestAsk = orderBook.Asks.First().Price;

       // Проверка threshold
       if (Math.Abs(bestBid - _orderState.BidPrice) / _orderState.BidPrice > _threshold) {
           await ModifyBidOrder(bestBid);
       }

       // Если есть позиция - обновляем ASK
       if (_positionManager.HasOpenPosition()) {
           var targetAsk = _positionManager.CalculateTargetPrice(...);
           if (ShouldUpdateAsk(targetAsk)) {
               await ModifyAskOrder(targetAsk);
           }
       }
   }
   ```

3. **Добавить debounce механизм:**
   - Не модифицировать чаще 1 раза в 200ms
   - Использовать DateTime.UtcNow для контроля частоты

4. **Логика обработки заполненных ордеров:**
   ```csharp
   OnOrderFilled(order) {
       if (order.Side == Buy) {
           _positionManager.OpenPosition(symbol, order.Price, order.Quantity, targetSpread);
           // Разместить ASK по целевой цене
       }
       else {
           _positionManager.ClosePosition();
           // Разместить новый BID
       }
   }
   ```

### Файлы для изменения:
- `src/Trader.Core/TradingStrategyService.cs` - полная переработка
- `src/Trader.ConsoleHost/Program.cs` - добавить PositionManager в DI

### Критерий завершения:
- ✅ Ордера модифицируются вместо отмены
- ✅ Threshold работает корректно
- ✅ Position отслеживается через PositionManager
- ✅ Логи показывают корректное поведение

---

## 📦 SPRINT 6: Улучшение Circuit Breaker
**Время:** 2-3 часа
**Приоритет:** СРЕДНИЙ

### Задачи:
1. **Добавить счетчик срабатываний:**
   ```csharp
   private int _circuitBreakerTripCount = 0;
   private DateTime? _lastTripTime;
   ```

2. **Реализовать cooldown:**
   ```csharp
   if (_lastTripTime.HasValue &&
       (DateTime.UtcNow - _lastTripTime.Value).TotalSeconds < _cooldownSeconds) {
       // В режиме cooldown - не торговать
       return;
   }
   ```

3. **Проверка волатильности:**
   ```csharp
   private Queue<decimal> _lastSpreads = new Queue<decimal>(10);

   var avgChange = CalculateAverageSpreadChange(_lastSpreads);
   if (avgChange > 0.5m) { // 0.5% средняя волатильность
       TriggerCircuitBreaker();
   }
   ```

4. **Расширенное логирование:**
   - Логировать каждое срабатывание
   - Логировать выход из cooldown
   - Метрики: количество срабатываний, средняя волатильность

### Критерий завершения:
- ✅ Circuit Breaker срабатывает при аномалиях
- ✅ Cooldown работает корректно
- ✅ Логи детальные и информативные

---

## 📦 SPRINT 7: Zero-Copy оптимизации
**Время:** 3-4 часа
**Приоритет:** НИЗКИЙ (после тестирования основной функциональности)

### Задачи:
1. **Добавить JSON Source Generation:**
   ```csharp
   [JsonSerializable(typeof(MexcOrderBook))]
   [JsonSerializable(typeof(MexcOrder))]
   [JsonSerializable(typeof(MexcStreamOrderBook))]
   internal partial class TradingJsonContext : JsonSerializerContext
   {
   }
   ```

2. **Интегрировать в WebSocket клиент:**
   - Обновить MexcSocketApiClient
   - Использовать Source Generated контекст для десериализации

3. **Профилирование:**
   - Запустить dotMemory
   - Проверить allocations в hot paths
   - Оптимизировать критические места

4. **Использовать Span<T> где возможно:**
   - Парсинг строк
   - Работа с массивами данных

### Критерий завершения:
- ✅ Allocations сокращены на 30%+
- ✅ Нет boxing/unboxing в hot paths
- ✅ Профилировщик показывает улучшения

---

## 🧪 SPRINT 8: Тестирование (ОБЯЗАТЕЛЬНО)
**Время:** 4-6 часов
**Приоритет:** ВЫСОКИЙ (перед production)

### Задачи:
1. **Unit тесты:**
   - `PositionManagerTests` - все методы
   - `OrderStateTests` - SetBid/Ask, Clear
   - `CircuitBreakerTests` - срабатывание, cooldown

2. **Integration тесты:**
   - Создать Mock для IMexcRestApiClient
   - Создать Mock для IMexcSocketApiClient
   - Тестировать TradingStrategyService end-to-end

3. **Live тестирование:**
   - Запустить на testnet MEXC (если есть)
   - Если нет testnet - запустить на production с 5 USDT
   - Мониторинг логов в real-time
   - Проверка всех сценариев:
     - Открытие позиции
     - Закрытие позиции
     - Модификация ордеров
     - Circuit Breaker
     - Обработка ошибок API

4. **Stress тестирование:**
   - Запустить на волатильной паре
   - Проверить поведение при rate limits
   - Проверить при сетевых ошибках

### Критерий завершения:
- ✅ 80%+ code coverage
- ✅ Все сценарии покрыты тестами
- ✅ 8+ часов live trading без ошибок
- ✅ Прибыльные сделки > убыточных

---

## 📋 Чеклист готовности к Production

- [ ] SPRINT 3 завершен и протестирован
- [ ] SPRINT 6 завершен
- [ ] SPRINT 8 (тесты) завершен
- [ ] 24+ часа live trading на малых суммах
- [ ] Нет критических багов
- [ ] Логирование детальное
- [ ] Мониторинг настроен
- [ ] API credentials в secrets manager (не в config!)
- [ ] Документация обновлена
- [ ] Code review пройден

---

## ⚠️ Критические замечания

1. **НЕ запускать на production до завершения Sprint 3**
   Текущая версия отменяет ордера при каждом обновлении стакана - это неэффективно и может привести к rate limits.

2. **Обязательно протестировать на testnet или малых суммах**
   Реальные деньги только после 24+ часов успешного тестирования.

3. **Мониторить rate limits MEXC API**
   Добавить логирование оставшихся лимитов запросов.

4. **Не забыть про Keep-Alive для User Stream**
   WebSocket соединение нужно поддерживать живым (ping каждые 30 сек).

---

## 🎯 Рекомендуемый порядок выполнения:

1. **SPRINT 3** (критично) - переписать торговую логику
2. **SPRINT 8** (часть 1) - unit тесты
3. **SPRINT 8** (часть 2) - integration тесты
4. **SPRINT 8** (часть 3) - live тестирование 5 USDT
5. **SPRINT 6** - улучшить Circuit Breaker
6. **SPRINT 8** (часть 4) - extended live тестирование
7. **SPRINT 7** - оптимизации (опционально)

---

## 📞 Контакты для вопросов

При возникновении вопросов или проблем:
- Проверить логи в `logs/trader-*.log`
- Проверить DEVELOPMENT_STATUS.md
- Изучить код примеров в `docs/Mexc.Net-main/Examples/`

