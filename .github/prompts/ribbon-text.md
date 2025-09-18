# Отображение текста в Revit Ribbon для debug-целей

Да, в Revit API существует несколько способов создания текстовых элементов в Ribbon для отображения статуса или отладочной информации. Вот основные подходы:

## **TextBox - Основной способ**

Наиболее подходящий контрол для отображения динамического текста - это **TextBox**. Он позволяет создать текстовое поле в ribbon панели, которое можно обновлять программно[1][2].

### Создание TextBox:

```csharp
public Result OnStartup(UIControlledApplication application)
{
    // Создаем ribbon panel
    RibbonPanel ribbonPanel = application.CreateRibbonPanel("Debug Panel");
    
    // Создаем TextBox для отображения статуса
    TextBoxData textData = new TextBoxData("StatusTextBox");
    textData.Name = "Status";
    textData.ToolTip = "Debug status information";
    textData.LongDescription = "Показывает текущий статус операций";
    textData.PromptText = "Ready..."; // Текст-подсказка
    
    // Добавляем TextBox в панель
    IList<RibbonItem> stackedItems = ribbonPanel.AddStackedItems(textData);
    TextBox statusTextBox = stackedItems[0] as TextBox;
    
    // Сохраняем ссылку для последующего обновления
    MyAddin.StatusTextBox = statusTextBox;
    
    return Result.Succeeded;
}
```

## **Динамическое обновление текста**

Для обновления текста в TextBox используется свойство **Value**[3]:

```csharp
public static class DebugHelper
{
    public static TextBox StatusTextBox { get; set; }
    
    public static void UpdateStatus(string message)
    {
        if (StatusTextBox != null)
        {
            StatusTextBox.Value = DateTime.Now.ToString("HH:mm:ss") + ": " + message;
        }
    }
}

// Использование в коде:
DebugHelper.UpdateStatus("Processing elements...");
DebugHelper.UpdateStatus("Transaction completed");
```

## **Альтернативный подход - Disabled Button как Label**

Если нужен статический текст-лейбл, можно использовать отключенную кнопку как метку[4][5]:

```csharp
// Создаем кнопку как лейбл
PushButtonData labelData = new PushButtonData(
    "StatusLabel", 
    "Status: Ready", 
    assembly, 
    "DummyCommand"); // Команда-заглушка

PushButton labelButton = ribbonPanel.AddItem(labelData) as PushButton;
labelButton.Enabled = false; // Отключаем кнопку
labelButton.ItemText = "Debug Info"; // Устанавливаем текст

// Для обновления текста:
labelButton.ItemText = "New Status: Processing";
```

## **Использование PromptText для дополнительной информации**

TextBox поддерживает **PromptText** - текст-подсказку, который отображается когда поле пустое[3]:

```csharp
statusTextBox.PromptText = "Debug output will appear here";
statusTextBox.Width = 200; // Устанавливаем ширину
```

## **Обработка событий TextBox**

Можно подписаться на события для интерактивности[6]:

```csharp
// Подписка на событие нажатия Enter
statusTextBox.EnterPressed += new EventHandler<TextBoxEnterPressedEventArgs>(OnEnterPressed);

private void OnEnterPressed(object sender, TextBoxEnterPressedEventArgs e)
{
    var textBox = sender as TextBox;
    string userInput = textBox.Value?.ToString();
    
    // Обработка пользовательского ввода
    ProcessDebugCommand(userInput);
    
    // Очистка поля после обработки
    textBox.Value = string.Empty;
}
```

## **Практический пример для debug-целей**

```csharp
public class DebugStatusManager
{
    private static TextBox _debugTextBox;
    private static Queue<string> _messageHistory = new Queue<string>();
    private const int MAX_MESSAGES = 5;
    
    public static void Initialize(TextBox textBox)
    {
        _debugTextBox = textBox;
        _debugTextBox.Width = 250;
        _debugTextBox.PromptText = "Debug messages...";
    }
    
    public static void LogMessage(string message)
    {
        if (_debugTextBox == null) return;
        
        string timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
        
        // Добавляем новое сообщение в историю
        _messageHistory.Enqueue(timestampedMessage);
        
        // Ограничиваем количество сообщений
        while (_messageHistory.Count > MAX_MESSAGES)
        {
            _messageHistory.Dequeue();
        }
        
        // Обновляем отображение
        _debugTextBox.Value = string.Join("\n", _messageHistory);
    }
}

// Использование:
DebugStatusManager.LogMessage("Command started");
DebugStatusManager.LogMessage("Processing 150 elements");
DebugStatusManager.LogMessage("Transaction committed");
```

## **Ключевые особенности**

1. **TextBox.Value** - основное свойство для установки/получения текста[3]
2. **PromptText** - текст-подсказка для пустого поля[3]
3. **Width** - ширина текстового поля[3]
4. **EnterPressed** - событие нажатия Enter[6]
5. **ShowImageAsButton** - позволяет добавить кликабельную иконку[3]
