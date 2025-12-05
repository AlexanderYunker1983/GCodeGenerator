# GCodeGenerator

Simple G-code generator for CNC machines with graphical interface and 3D trajectory visualization.

## Description

GCodeGenerator is a Windows application that allows you to quickly create G-code programs for CNC machines through a convenient graphical interface without being tied to CAD systems. The application supports various types of operations (drilling, milling) and includes 3D preview for visualizing tool trajectories.

## Main Features

### Drilling Operations
- **Point drilling** — creating holes at specified coordinates
- **Line drilling** — uniform distribution of holes along a line
- **Array drilling** — creating a grid of holes
- **Rectangle drilling** — holes along the perimeter of a rectangle
- **Circle drilling** — holes along a circle
- **Batch drilling** — using predefined templates

### Milling Operations
- **Profile milling** — processing rectangular contours
- **Pocket milling** (in development)

### Additional Features
- **3D trajectory visualization** — interactive viewing of tool trajectories
- **G-code preview** — viewing generated code before saving
- **Generation settings** — flexible configuration of output G-code format
- **Localization** — Russian language support, other languages in development
- **Operation management** — adding, removing, changing operation order

## Requirements

- Windows 7 or higher
- .NET Framework 4.8.1
- Visual Studio 2022 or higher (for building from source)
*Other dependencies are automatically pulled from github when generating the project (see below)

## Installation

### Ready Builds
Download the latest installer version from the [Releases](https://github.com/yourusername/GCodeGenerator/releases) section and run the installer.

### Building from Source

1. Clone the repository:
```bash
git clone https://github.com/yourusername/GCodeGenerator.git
cd GCodeGenerator
```

2. Restore NuGet dependencies and generate files:
```bash
gen-gcodegenerator.cmd
```

3. Open `GCodeGenerator.sln` in Visual Studio and build the solution.

### Building the Installer

1. Set a new git tag in the format x.y.z
If z is not equal to 0, it is considered a testing build

2. Run the build script
```bash
gen-gcodegenerator_install.cmd
```

3. After the build completes, the installer file named * GCodeGenerator_ru_RU_[tag].exe will be located in the directory * _build_GCODEGENERATOR_NMake_VS17/_install/Release *

## Usage

1. **Launch the application** GCodeGenerator.exe

2. **Select the operation type** from the tabs in the left part of the window:
   - Drilling
   - Milling

3. **Add an operation**:
   - Click on the button of the desired operation type
   - Fill in the parameters in the opened dialog
   - The operation will be added to the list

4. **Configure parameters** (optional):
   - Click the "Settings" button to change G-code generation parameters

5. **Generate G-code**:
   - Click the "Generate G-code" button
   - View the result in the right panel

6. **View 3D preview** (optional):
   - Click the "G-code Preview" button to visualize trajectories

7. **Save the result**:
   - Click the "Save G-code" button
   - Choose a location to save the file

## Project Structure

```
GCodeGenerator/
├── GCodeGenerator/          # Main application
│   ├── Models/              # Data models
│   ├── ViewModels/          # ViewModels (MVVM)
│   ├── Views/               # Views (XAML)
│   ├── Services/            # G-code generation services
│   └── Infrastructure/      # Helper classes
├── YLocalization/           # Localization module
├── YMugenExtensions/        # Extensions for MugenMvvmToolkit
├── install/                 # Installer scripts
└── CMakeLists.txt           # CMake configuration file
```

## Technologies

- **.NET Framework 4.8.1** — development platform
- **WPF** — graphical interface
- **MugenMvvmToolkit** — MVVM framework
- **Autofac** — dependency container
- **CMake** — build system
- **YBuild** — build system and dependency management

## License

This project is distributed under the MIT license. See the [LICENSE](LICENSE) file for details.

## Author

Copyright (c) 2021 Alexander Yunker

## Contributing

Contributions to the project are welcome! Please:

1. Fork the project
2. Create a branch for a new feature (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Known Issues and Plans

- [ ] Implementation of pocket milling
- [ ] Support for additional operation types
- [ ] Export to various formats
- [ ] Import from CAD systems

## Support

If you have questions or issues, please create an [Issue](https://github.com/yourusername/GCodeGenerator/issues) in the repository.

---

# GCodeGenerator

Простой генератор G-кода для станков с ЧПУ с графическим интерфейсом и 3D-визуализацией траекторий.

## Описание

GCodeGenerator — это приложение для Windows, которое позволяет быстро создавать G-код программы для станков с ЧПУ через удобный графический интерфейс без привязки к CAD-системам. Приложение поддерживает различные типы операций (сверление, фрезерование) и включает в себя 3D-превью для визуализации траекторий инструмента.

## Основные возможности

### Операции сверления
- **Сверление по точкам** — создание отверстий в указанных координатах
- **Сверление по линии** — равномерное распределение отверстий вдоль линии
- **Сверление по массиву** — создание сетки отверстий
- **Сверление по прямоугольнику** — отверстия по периметру прямоугольника
- **Сверление по кругу** — отверстия по окружности
- **Сверление по пакету** — использование предопределенных шаблонов

### Операции фрезерования
- **Профильное фрезерование** — обработка контуров прямоугольников
- **Фрезерование карманов** (в разработке)

### Дополнительные функции
- **3D-визуализация траекторий** — интерактивный просмотр траекторий инструмента
- **Предпросмотр G-кода** — просмотр сгенерированного кода перед сохранением
- **Настройки генерации** — гибкая настройка формата выходного G-кода
- **Локализация** — поддержка русского языка, другие языки в разработке
- **Управление операциями** — добавление, удаление, изменение порядка операций

## Требования

- Windows 7 или выше
- .NET Framework 4.8.1
- Visual Studio 2022 или выше (для сборки из исходников)
*Остальные зависимости автоматически стягиваются с github при генерации проекта (см. ниже)

## Установка

### Готовые сборки
Скачайте последнюю версию установщика из раздела [Releases](https://github.com/yourusername/GCodeGenerator/releases) и запустите установщик.

### Сборка из исходников

1. Клонируйте репозиторий:
```bash
git clone https://github.com/yourusername/GCodeGenerator.git
cd GCodeGenerator
```

2. Восстановите зависимости NuGet и генерация файлов:
```bash
gen-gcodegenerator.cmd
```

3. Откройте `GCodeGenerator.sln` в Visual Studio и соберите решение.

### Сборка инсталлятора

1. Проставьте новый тег git-а в формате x.y.z
Если z не равно 0, считается что это сборка для тестирования

2. Запустите скрипт сборки
```bash
gen-gcodegenerator_install.cmd
```

3. После окончания сборки в каталоге * _build_GCODEGENERATOR_NMake_VS17/_install/Release * будет находиться файл инсталлятора с именем * GCodeGenerator_ru_RU_[тег].exe

## Использование

1. **Запустите приложение** GCodeGenerator.exe

2. **Выберите тип операции** из вкладок в левой части окна:
   - Сверление
   - Фрезерование

3. **Добавьте операцию**:
   - Нажмите на кнопку нужного типа операции
   - Заполните параметры в открывшемся диалоге
   - Операция будет добавлена в список

4. **Настройте параметры** (опционально):
   - Нажмите кнопку "Настройки" для изменения параметров генерации G-кода

5. **Сгенерируйте G-код**:
   - Нажмите кнопку "Сгенерировать G-код"
   - Просмотрите результат в правой панели

6. **Просмотрите 3D-превью** (опционально):
   - Нажмите кнопку "Превью G-кода" для визуализации траекторий

7. **Сохраните результат**:
   - Нажмите кнопку "Сохранить G-код"
   - Выберите место для сохранения файла

## Структура проекта

```
GCodeGenerator/
├── GCodeGenerator/          # Основное приложение
│   ├── Models/              # Модели данных
│   ├── ViewModels/          # ViewModel'и (MVVM)
│   ├── Views/               # Представления (XAML)
│   ├── Services/            # Сервисы генерации G-кода
│   └── Infrastructure/      # Вспомогательные классы
├── YLocalization/           # Модуль локализации
├── YMugenExtensions/        # Расширения для MugenMvvmToolkit
├── install/                 # Скрипты установщика
└── CMakeLists.txt           # Файл конфигурации CMake
```

## Технологии

- **.NET Framework 4.8.1** — платформа разработки
- **WPF** — графический интерфейс
- **MugenMvvmToolkit** — фреймворк для MVVM
- **Autofac** — контейнер зависимостей
- **CMake** — система сборки
- **YBuild** — система сборки и управления зависимостями

## Лицензия

Этот проект распространяется под лицензией MIT. См. файл [LICENSE](LICENSE) для подробностей.

## Автор

Copyright (c) 2021 Alexander Yunker

## Вклад в проект

Вклад в проект приветствуется! Пожалуйста:

1. Создайте форк проекта
2. Создайте ветку для новой функции (`git checkout -b feature/AmazingFeature`)
3. Зафиксируйте изменения (`git commit -m 'Add some AmazingFeature'`)
4. Отправьте в ветку (`git push origin feature/AmazingFeature`)
5. Откройте Pull Request

## Известные проблемы и планы

- [ ] Реализация фрезерования карманов
- [ ] Поддержка дополнительных типов операций
- [ ] Экспорт в различные форматы
- [ ] Импорт из CAD-систем

## Поддержка

Если у вас возникли вопросы или проблемы, пожалуйста, создайте [Issue](https://github.com/yourusername/GCodeGenerator/issues) в репозитории.

