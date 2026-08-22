using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Служба сохранения/загрузки файлов проекта .ygc (пункт 0.6 плана).
    /// Вынесена из MainViewModel: сериализация операций в JSON, разрешение типов,
    /// пропуск некорректных записей. Формат файла не изменён (JavaScriptSerializer, UTF-8):
    /// <code>{"Operations":[{"Type":"&lt;AssemblyQualifiedName&gt;","Data":"&lt;JSON операции&gt;"}]}</code>
    /// Старые .ygc файлы остаются читаемыми, новые — читаемы старой версией.
    /// </summary>
    public class ProjectFileService
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        /// <summary>
        /// Сериализует операции в JSON проекта .ygc (in-memory).
        /// </summary>
        /// <param name="operations">Операции в том порядке, в котором они должны сохраниться.</param>
        public string Serialize(IReadOnlyList<OperationBase> operations)
        {
            var project = new ProjectData
            {
                Operations = operations.Select(op => new SerializableOperation
                {
                    Type = op.GetType().AssemblyQualifiedName,
                    Data = _serializer.Serialize(op)
                }).ToList()
            };

            return _serializer.Serialize(project);
        }

        /// <summary>Сохраняет операции в файл (UTF-8).</summary>
        public void Save(string filePath, IReadOnlyList<OperationBase> operations)
        {
            File.WriteAllText(filePath, Serialize(operations), Encoding.UTF8);
        }

        /// <summary>
        /// Читает проект из файла.
        /// Бросает исключение при некорректном JSON — обработчик ошибки остаётся у вызывающего
        /// (как раньше в MainViewModel.OpenProject).
        /// </summary>
        public ProjectData Load(string filePath)
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return Deserialize(json);
        }

        /// <summary>
        /// Десериализует JSON проекта .ygc в <see cref="ProjectData"/>.
        /// Бросает исключение при некорректном JSON.
        /// </summary>
        public ProjectData Deserialize(string json)
        {
            return _serializer.Deserialize<ProjectData>(json);
        }

        /// <summary>
        /// Извлекает операции из проекта, пропуская некорректные записи:
        /// пустые Type/Data, неизвестный тип, десериализация не в OperationBase.
        /// Повторяет поведение прежнего MainViewModel.LoadOperationsFromProject.
        /// </summary>
        public List<OperationBase> ExtractOperations(ProjectData project)
        {
            var result = new List<OperationBase>();
            if (project?.Operations == null)
                return result;

            foreach (var opDto in project.Operations)
            {
                if (string.IsNullOrWhiteSpace(opDto?.Type) || string.IsNullOrWhiteSpace(opDto.Data))
                    continue;

                var type = Type.GetType(opDto.Type);
                if (type == null)
                    continue;

                var operation = _serializer.Deserialize(opDto.Data, type) as OperationBase;
                if (operation == null)
                    continue;

                result.Add(operation);
            }

            return result;
        }
    }

    /// <summary>Структура файла проекта .ygc.</summary>
    public class ProjectData
    {
        public List<SerializableOperation> Operations { get; set; }
    }

    /// <summary>Запись операции в проекте: тип (AssemblyQualifiedName) + JSON-данные.</summary>
    public class SerializableOperation
    {
        public string Type { get; set; }
        public string Data { get; set; }
    }
}
