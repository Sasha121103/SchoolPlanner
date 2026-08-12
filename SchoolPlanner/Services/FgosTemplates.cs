using System.Collections.Generic;
using SchoolPlanner.Database;

namespace SchoolPlanner.Services
{
    public class FgosTemplates
    {
        public static List<FgosTemplate> GetTemplates()
        {
            return new List<FgosTemplate>
            {
                new FgosTemplate
                {
                    Subject = "Математика",
                    Grade = "5",
                    Topic = "Обыкновенные дроби",
                    Goal = "Сформировать понятие обыкновенной дроби, научить читать и записывать дроби, решать простейшие задачи",
                    Tasks = new List<string>
                    {
                        "Познакомить с понятием дроби, числителя и знаменателя",
                        "Научить сравнивать дроби с одинаковыми знаменателями",
                        "Развить навыки решения задач на нахождение части числа"
                    },
                    Stages = new List<LessonStage>
                    {
                        new LessonStage
                        {
                            Name = "Организационный момент",
                            Duration = 3,
                            Description = "Приветствие, проверка готовности к уроку",
                            Example = "Здравствуйте, ребята! Проверьте, всё ли готово к уроку"
                        },
                        new LessonStage
                        {
                            Name = "Актуализация знаний",
                            Duration = 7,
                            Description = "Устный счет, повторение деления и долей",
                            Example = "Разделите 6 яблок на 3 равные части. Сколько получится?"
                        },
                        new LessonStage
                        {
                            Name = "Изучение нового материала",
                            Duration = 15,
                            Description = "Объяснение понятия дроби, запись дробей",
                            Example = "Показ на примере пиццы: разделим на 4 части, возьмём 1 часть - это 1/4"
                        },
                        new LessonStage
                        {
                            Name = "Закрепление",
                            Duration = 15,
                            Description = "Решение упражнений, работа с учебником",
                            Example = "№ 1, 3, 5 на стр. 25"
                        },
                        new LessonStage
                        {
                            Name = "Итог урока",
                            Duration = 5,
                            Description = "Вопросы, рефлексия, домашнее задание",
                            Example = "Что нового узнали? Какие задания были сложными?"
                        }
                    },
                    Source = "edsoo.ru"
                },
                new FgosTemplate
                {
                    Subject = "Русский язык",
                    Grade = "6",
                    Topic = "Имя существительное как часть речи",
                    Goal = "Систематизировать и углубить знания об имени существительном",
                    Tasks = new List<string>
                    {
                        "Обобщить знания о грамматических признаках существительного",
                        "Развить умение определять род, число, падеж",
                        "Совершенствовать навыки морфологического разбора"
                    },
                    Stages = new List<LessonStage>
                    {
                        new LessonStage
                        {
                            Name = "Оргмомент",
                            Duration = 2,
                            Description = "Приветствие, настрой на работу",
                            Example = ""
                        },
                        new LessonStage
                        {
                            Name = "Проверка ДЗ",
                            Duration = 5,
                            Description = "Выборочная проверка, разбор ошибок",
                            Example = "Упр. 120: проверка цепочкой"
                        },
                        new LessonStage
                        {
                            Name = "Повторение",
                            Duration = 10,
                            Description = "Фронтальный опрос по теме",
                            Example = "Что такое существительное? Какие бывают разряды?"
                        },
                        new LessonStage
                        {
                            Name = "Практикум",
                            Duration = 20,
                            Description = "Упражнения, работа в парах",
                            Example = "Найти существительные в тексте, определить признаки"
                        },
                        new LessonStage
                        {
                            Name = "Итог",
                            Duration = 8,
                            Description = "Самостоятельная работа, выводы",
                            Example = "Выполнить морфологический разбор 2 слов"
                        }
                    },
                    Source = "prosv.ru"
                }
            };
        }
    }
}