using System;
using System.Collections.Generic;
using System.Linq;
using SchoolPlanner.Database;

namespace SchoolPlanner.Services
{
    public class ScheduleGenerator
    {
        private readonly Random _random = new Random();

        private class LessonRequest
        {
            public string Class { get; set; }
            public int ClassGrade { get; set; }
            public string Subject { get; set; }
            public int Duration { get; set; }
            public int Difficulty { get; set; }
            public int HoursPerWeek { get; set; }
            public bool IsAssigned { get; set; } = false;
            public bool IsFiller { get; set; } = false;
            public string DayCategory { get; set; }
            public bool IsMondayOnly { get; set; } = false;
        }

        public class TeacherInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Subject { get; set; }
            public int MaxHoursPerWeek { get; set; } = 20;
            public string Room { get; set; }
            public List<string> Classes { get; set; } = new List<string>();
            public Dictionary<string, List<string>> SubjectsByClass { get; set; } = new Dictionary<string, List<string>>();
            public Dictionary<string, HashSet<string>> SubjectClassCache { get; set; } = new Dictionary<string, HashSet<string>>();
            public HashSet<string> DaysOff { get; set; } = new HashSet<string>();

            public bool IsWorking(string dayOfWeek)
            {
                return !DaysOff.Contains(dayOfWeek);
            }

            public bool HasSubjectInClass(string subject, string className)
            {
                if (SubjectClassCache.TryGetValue(subject, out var classes))
                    return classes.Contains(className);
                return false;
            }
        }

        public class SubjectDifficulty
        {
            public int Id { get; set; }
            public string SubjectName { get; set; }
            public int Grade5 { get; set; }
            public int Grade6 { get; set; }
            public int Grade7 { get; set; }
            public int Grade8 { get; set; }
            public int Grade9 { get; set; }
            public int Grade10 { get; set; }
            public int Grade11 { get; set; }
        }

        public class GenerationSettings
        {
            public string ScheduleName { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public int LessonsPerDay { get; set; } = 7;
            public int StartHour { get; set; } = 8;
            public int StartMinute { get; set; } = 30;
            public int LessonDuration { get; set; } = 40;
            public int BreakDuration { get; set; } = 10;
            public List<string> Classes { get; set; }
            public List<string> ExcludedDays { get; set; } = new List<string>();
            public bool RespectTeacherHours { get; set; } = true;
            public bool ConsiderDifficulty { get; set; } = true;
            public bool RespectTeacherDaysOff { get; set; } = true;
        }

        public class GenerationResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public Schedule GeneratedSchedule { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();
            public Dictionary<string, int> Statistics { get; set; } = new Dictionary<string, int>();
            public Dictionary<string, int> TeacherWorkload { get; set; } = new Dictionary<string, int>();
            public Dictionary<string, int> ClassLoad { get; set; } = new Dictionary<string, int>();
            public Dictionary<string, Dictionary<string, int>> SubjectDistribution { get; set; } = new Dictionary<string, Dictionary<string, int>>();
        }

        // ============================================================
        // ГЛАВНЫЙ МЕТОД - ГЕНЕРАЦИЯ ДЛЯ ВСЕХ КЛАССОВ
        // ============================================================
        public GenerationResult GenerateScheduleForAllClasses(GenerationSettings settings, DbHelper dbHelper)
        {
            var allClasses = dbHelper.GetAllClasses();

            if (allClasses == null || !allClasses.Any())
            {
                return new GenerationResult
                {
                    Success = false,
                    Message = "Нет классов в базе данных"
                };
            }

            settings.Classes = allClasses.Select(c => c.Name).ToList();

            System.Diagnostics.Debug.WriteLine($"=== ГЕНЕРАЦИЯ ДЛЯ ВСЕХ КЛАССОВ ===");
            System.Diagnostics.Debug.WriteLine($"Найдено классов: {settings.Classes.Count}");

            return GenerateSchedule(settings, dbHelper);
        }

        // ============================================================
        // ОСНОВНОЙ МЕТОД ГЕНЕРАЦИИ
        // ============================================================
        public GenerationResult GenerateSchedule(GenerationSettings settings, DbHelper dbHelper)
        {
            var result = new GenerationResult();
            var warnings = new List<string>();

            try
            {
                System.Diagnostics.Debug.WriteLine("=== НАЧАЛО ГЕНЕРАЦИИ РАСПИСАНИЯ ===");

                if (settings.Classes == null || !settings.Classes.Any())
                {
                    var allClasses = dbHelper.GetAllClasses();
                    if (allClasses != null && allClasses.Any())
                    {
                        settings.Classes = allClasses.Select(c => c.Name).ToList();
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Нет классов для генерации расписания";
                        return result;
                    }
                }

                var schedule = new Schedule
                {
                    Name = settings.ScheduleName,
                    StartDate = settings.StartDate,
                    EndDate = settings.EndDate,
                    Status = ScheduleStatus.Draft,
                    CreatedAt = DateTime.Now,
                    IsActive = true,
                    Lessons = new List<ScheduleLesson>()
                };

                var allDays = new List<string> { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница" };
                var availableDays = allDays.Where(d => !settings.ExcludedDays.Contains(d)).ToList();

                if (!availableDays.Any())
                {
                    availableDays = allDays;
                    warnings.Add("Все дни были исключены, используются все дни недели ПН-ПТ");
                }

                var lightDays = availableDays.Where(d => GetDayCategory(d) == "light").ToList();
                var heavyDays = availableDays.Where(d => GetDayCategory(d) == "heavy").ToList();

                var subjectDifficulties = dbHelper.GetAllSubjectDifficulties();
                var difficultyDict = new Dictionary<string, SubjectDifficulty>();
                foreach (var d in subjectDifficulties)
                {
                    if (!difficultyDict.ContainsKey(d.SubjectName))
                    {
                        difficultyDict[d.SubjectName] = ConvertToGeneratorDifficulty(d);
                    }
                }

                var allTeachers = ConvertToGeneratorTeachers(dbHelper.GetAllTeachersWithDetails(), dbHelper);

                var teachersBySubject = new Dictionary<string, List<TeacherInfo>>();
                foreach (var t in allTeachers)
                {
                    if (!string.IsNullOrEmpty(t.Subject))
                    {
                        if (!teachersBySubject.ContainsKey(t.Subject))
                            teachersBySubject[t.Subject] = new List<TeacherInfo>();
                        teachersBySubject[t.Subject].Add(t);
                    }
                }

                // ============================================================
                // ЗАГРУЖАЕМ КАБИНЕТЫ (ОДИН РАЗ)
                // ============================================================
                var allRooms = dbHelper.GetAllRooms();
                var roomMapBySubject = new Dictionary<string, List<string>>();
                var universalRooms = new List<string>();

                foreach (var r in allRooms)
                {
                    if (string.IsNullOrEmpty(r.Subject) || r.Subject == "Универсальный")
                    {
                        universalRooms.Add(r.Number);
                    }
                    else
                    {
                        if (!roomMapBySubject.ContainsKey(r.Subject))
                            roomMapBySubject[r.Subject] = new List<string>();
                        roomMapBySubject[r.Subject].Add(r.Number);
                    }
                }

                var teacherWorkload = new Dictionary<string, int>();
                foreach (var t in allTeachers)
                {
                    teacherWorkload[t.Name] = 0;
                }

                var subjectDistribution = new Dictionary<string, Dictionary<string, int>>();
                var classPlans = new Dictionary<string, Dictionary<string, int>>();
                var classMaxLessons = new Dictionary<string, int>();
                var classGrades = new Dictionary<string, int>();
                var validClasses = new List<string>();
                var subjectCategoryCache = new Dictionary<string, Dictionary<string, string>>();
                var subjectDifficultyCache = new Dictionary<string, Dictionary<string, int>>();

                // ============================================================
                // ЗАГРУЗКА УЧЕБНЫХ ПЛАНОВ
                // ============================================================
                foreach (var className in settings.Classes)
                {
                    var plan = dbHelper.GetPlanForClass(className);
                    if (plan == null)
                    {
                        warnings.Add($"Для класса {className} не найден учебный план");
                        continue;
                    }

                    int grade = GetGradeFromClassName(className);
                    int maxPerDay = GetMaxLessonsPerDay(grade);
                    int maxPerWeek = GetMaxLessonsPerWeek(grade);

                    classMaxLessons[className] = maxPerDay;
                    classGrades[className] = grade;
                    subjectDistribution[className] = new Dictionary<string, int>();
                    classPlans[className] = new Dictionary<string, int>();
                    validClasses.Add(className);
                    subjectCategoryCache[className] = new Dictionary<string, string>();
                    subjectDifficultyCache[className] = new Dictionary<string, int>();

                    var classSubjects = plan.Subjects;
                    int totalHours = classSubjects.Sum(s => s.HoursPerWeek);

                    if (totalHours > maxPerWeek)
                    {
                        warnings.Add($"⚠️ Класс {className}: нагрузка {totalHours} ч > макс {maxPerWeek} ч");
                    }

                    foreach (var subject in classSubjects)
                    {
                        int hoursPerWeek = subject.HoursPerWeek;
                        string correctedName = CorrectSubjectName(subject.SubjectName, grade);

                        int difficulty = GetSubjectDifficultyFromDB(correctedName, grade, difficultyDict);
                        if (difficulty == 0 || difficulty == 5)
                        {
                            difficulty = subject.Difficulty > 0 ? subject.Difficulty : 5;
                        }

                        if (!classPlans[className].ContainsKey(correctedName))
                        {
                            classPlans[className][correctedName] = 0;
                        }
                        classPlans[className][correctedName] += hoursPerWeek;

                        if (!subjectDistribution[className].ContainsKey(correctedName))
                        {
                            subjectDistribution[className][correctedName] = 0;
                        }

                        if (!subjectDifficultyCache[className].ContainsKey(correctedName))
                        {
                            subjectDifficultyCache[className][correctedName] = difficulty;
                            subjectCategoryCache[className][correctedName] = GetSubjectCategory(difficulty);
                        }
                    }
                }

                if (!classPlans.Any())
                {
                    result.Success = false;
                    result.Message = "Нет классов с учебными планами для генерации";
                    return result;
                }

                // Сетка расписания
                var scheduleGrid = new Dictionary<string, Dictionary<string, HashSet<int>>>();
                foreach (var className in validClasses)
                {
                    scheduleGrid[className] = new Dictionary<string, HashSet<int>>();
                    foreach (var day in availableDays)
                    {
                        scheduleGrid[className][day] = new HashSet<int>();
                    }
                }

                // ============================================================
                // СОЗДАЕМ ВСЕ УРОКИ
                // ============================================================
                var allLessons = new List<LessonRequest>();
                foreach (var className in classPlans.Keys)
                {
                    int grade = classGrades[className];

                    foreach (var subject in classPlans[className])
                    {
                        string subjectName = subject.Key;
                        int hoursPerWeek = subject.Value;

                        int difficulty = 5;
                        if (subjectDifficultyCache.ContainsKey(className) &&
                            subjectDifficultyCache[className].ContainsKey(subjectName))
                        {
                            difficulty = subjectDifficultyCache[className][subjectName];
                        }

                        string category = GetSubjectCategory(difficulty);
                        bool isMondayOnly = subjectName == "Разговоры о важном";

                        for (int i = 0; i < hoursPerWeek; i++)
                        {
                            var lesson = new LessonRequest
                            {
                                Class = className,
                                ClassGrade = grade,
                                Subject = subjectName,
                                Difficulty = difficulty,
                                HoursPerWeek = hoursPerWeek,
                                IsAssigned = false,
                                IsFiller = false,
                                DayCategory = category,
                                IsMondayOnly = isMondayOnly
                            };
                            allLessons.Add(lesson);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Всего уроков для назначения: {allLessons.Count}");

                // ============================================================
                // ЛОКАЛЬНАЯ ФУНКЦИЯ ДЛЯ ПОИСКА КАБИНЕТА
                // ============================================================
                string FindRoomForSubject(string subject)
                {
                    if (roomMapBySubject.ContainsKey(subject) && roomMapBySubject[subject].Any())
                    {
                        return roomMapBySubject[subject].First();
                    }
                    if (universalRooms.Any())
                    {
                        return universalRooms.First();
                    }
                    return GenerateRoomNumber();
                }

                // ============================================================
                // ОСНОВНАЯ ЛОГИКА НАЗНАЧЕНИЯ УРОКА
                // ============================================================
                void AssignLessonWithResources(LessonRequest lesson, string day, int lessonNumber)
                {
                    if (lesson.IsMondayOnly && day != "Понедельник")
                    {
                        warnings.Add($"⚠️ {lesson.Subject} для {lesson.Class} должен быть только в понедельник");
                        return;
                    }

                    if (lesson.IsMondayOnly && !availableDays.Contains("Понедельник"))
                    {
                        warnings.Add($"⚠️ {lesson.Subject} для {lesson.Class} требует понедельник, но он исключен");
                        return;
                    }

                    if (!scheduleGrid.ContainsKey(lesson.Class) || !scheduleGrid[lesson.Class].ContainsKey(day))
                    {
                        warnings.Add($"Класс {lesson.Class} или день {day} отсутствует в сетке");
                        return;
                    }

                    var teacher = FindBestTeacher(
                        lesson.Subject,
                        lesson.Class,
                        lesson.ClassGrade,
                        teacherWorkload,
                        allTeachers,
                        teachersBySubject,
                        day,
                        settings.RespectTeacherDaysOff
                    );

                    if (teacher == null)
                    {
                        warnings.Add($"⚠️ Нет учителя для {lesson.Subject} в {lesson.Class} на {day}");

                        if (teachersBySubject.ContainsKey(lesson.Subject))
                        {
                            var anyTeacher = teachersBySubject[lesson.Subject]
                                .Where(t => t.MaxHoursPerWeek > (teacherWorkload.ContainsKey(t.Name) ? teacherWorkload[t.Name] : 0))
                                .OrderBy(t => teacherWorkload.ContainsKey(t.Name) ? teacherWorkload[t.Name] : 0)
                                .FirstOrDefault();

                            if (anyTeacher != null)
                            {
                                teacher = anyTeacher;
                                warnings.Add($"⚠️ Используется {teacher.Name} без прямой привязки к {lesson.Class}");
                            }
                        }

                        if (teacher == null)
                        {
                            teacher = new TeacherInfo
                            {
                                Id = 999,
                                Name = $"5Временный учитель ({lesson.Subject})",
                                Subject = lesson.Subject,
                                MaxHoursPerWeek = 40,
                                Room = GenerateRoomNumber()
                            };
                            allTeachers.Add(teacher);
                            if (!teacherWorkload.ContainsKey(teacher.Name))
                                teacherWorkload[teacher.Name] = 0;

                            if (!teacher.SubjectClassCache.ContainsKey(lesson.Subject))
                                teacher.SubjectClassCache[lesson.Subject] = new HashSet<string>();
                            teacher.SubjectClassCache[lesson.Subject].Add(lesson.Class);
                        }
                    }

                    if (settings.RespectTeacherHours && teacherWorkload.ContainsKey(teacher.Name) &&
                        teacherWorkload[teacher.Name] >= teacher.MaxHoursPerWeek)
                    {
                        var alternativeTeacher = FindBestTeacher(
                            lesson.Subject,
                            lesson.Class,
                            lesson.ClassGrade,
                            teacherWorkload,
                            allTeachers,
                            teachersBySubject,
                            day,
                            settings.RespectTeacherDaysOff
                        );

                        if (alternativeTeacher != null && alternativeTeacher.Name != teacher.Name)
                        {
                            teacher = alternativeTeacher;
                            warnings.Add($"ℹ️ {teacher.Name} заменен из-за перегрузки");
                        }
                    }

                    // Используем локальную функцию FindRoomForSubject
                    string room = !string.IsNullOrEmpty(teacher.Room) ? teacher.Room : FindRoomForSubject(lesson.Subject);

                    TimeSpan startTime = CalculateStartTime(lessonNumber, settings.StartHour, settings.StartMinute,
                        settings.LessonDuration, settings.BreakDuration);
                    TimeSpan endTime = CalculateEndTime(lessonNumber, settings.StartHour, settings.StartMinute,
                        settings.LessonDuration, settings.BreakDuration);

                    var newLesson = new ScheduleLesson
                    {
                        Id = schedule.Lessons.Count + 1,
                        Subject = lesson.Subject,
                        Class = lesson.Class,
                        Teacher = teacher.Name,
                        TeacherId = teacher.Id > 0 ? teacher.Id : 1,
                        DayOfWeek = day,
                        LessonNumber = lessonNumber,
                        StartTime = startTime,
                        EndTime = endTime,
                        Room = room,
                        Note = lesson.IsFiller ? "Автоматически добавлен" : (lesson.IsMondayOnly ? "Только понедельник" : ""),
                        Homework = ""
                    };

                    schedule.Lessons.Add(newLesson);
                    lesson.IsAssigned = true;

                    if (!teacherWorkload.ContainsKey(teacher.Name))
                        teacherWorkload[teacher.Name] = 0;
                    teacherWorkload[teacher.Name]++;

                    if (!subjectDistribution.ContainsKey(lesson.Class))
                        subjectDistribution[lesson.Class] = new Dictionary<string, int>();

                    if (!subjectDistribution[lesson.Class].ContainsKey(lesson.Subject))
                        subjectDistribution[lesson.Class][lesson.Subject] = 0;

                    subjectDistribution[lesson.Class][lesson.Subject]++;

                    scheduleGrid[lesson.Class][day].Add(lessonNumber);
                }

                // ============================================================
                // 1. "РАЗГОВОРЫ О ВАЖНОМ" - ТОЛЬКО ПОНЕДЕЛЬНИК
                // ============================================================
                var mondayOnlyLessons = allLessons
                    .Where(l => l.IsMondayOnly && !l.IsAssigned)
                    .ToList();

                foreach (var lesson in mondayOnlyLessons)
                {
                    if (!classMaxLessons.ContainsKey(lesson.Class)) continue;

                    int maxPerDay = classMaxLessons[lesson.Class];
                    bool assigned = false;
                    string day = "Понедельник";

                    if (availableDays.Contains(day))
                    {
                        if (!scheduleGrid[lesson.Class][day].Contains(1))
                        {
                            AssignLessonWithResources(lesson, day, 1);
                            assigned = true;
                        }
                        else
                        {
                            for (int num = 2; num <= maxPerDay; num++)
                            {
                                if (!scheduleGrid[lesson.Class][day].Contains(num))
                                {
                                    AssignLessonWithResources(lesson, day, num);
                                    assigned = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        warnings.Add($"⚠️ Понедельник исключен, {lesson.Subject} для {lesson.Class} не может быть назначен");
                    }

                    if (!assigned)
                    {
                        warnings.Add($"⚠️ Не удалось назначить {lesson.Subject} для {lesson.Class} в понедельник");
                    }
                }

                allLessons = allLessons.Where(l => !l.IsAssigned).ToList();

                // ============================================================
                // 2. ТЯЖЕЛЫЕ ПРЕДМЕТЫ (УТРО)
                // ============================================================
                var heavyLessons = allLessons
                    .Where(l => l.DayCategory == "heavy" && !l.IsAssigned)
                    .OrderByDescending(l => l.Difficulty)
                    .ToList();

                foreach (var lesson in heavyLessons)
                {
                    if (!classMaxLessons.ContainsKey(lesson.Class)) continue;
                    int maxPerDay = classMaxLessons[lesson.Class];
                    bool assigned = false;

                    foreach (var day in heavyDays.OrderBy(x => _random.Next()))
                    {
                        if (!scheduleGrid.ContainsKey(lesson.Class) || !scheduleGrid[lesson.Class].ContainsKey(day))
                            continue;

                        bool subjectAlreadyInDay = schedule.Lessons.Any(l =>
                            l.Class == lesson.Class &&
                            l.DayOfWeek == day &&
                            l.Subject == lesson.Subject);

                        if (subjectAlreadyInDay)
                            continue;

                        for (int num = 1; num <= Math.Min(3, maxPerDay); num++)
                        {
                            if (!scheduleGrid[lesson.Class][day].Contains(num))
                            {
                                AssignLessonWithResources(lesson, day, num);
                                assigned = true;
                                break;
                            }
                        }

                        if (assigned) break;
                    }

                    if (!assigned)
                    {
                        warnings.Add($"Не удалось назначить тяжелый урок {lesson.Class} - {lesson.Subject}");
                    }
                }

                allLessons = allLessons.Where(l => !l.IsAssigned).ToList();

                // ============================================================
                // 3. ЛЕГКИЕ ПРЕДМЕТЫ (КОНЕЦ ДНЯ)
                // ============================================================
                var lightLessons = allLessons
                    .Where(l => l.DayCategory == "light" && !l.IsAssigned)
                    .OrderBy(l => l.Difficulty)
                    .ToList();

                foreach (var lesson in lightLessons)
                {
                    if (!classMaxLessons.ContainsKey(lesson.Class)) continue;
                    int maxPerDay = classMaxLessons[lesson.Class];
                    bool assigned = false;

                    foreach (var day in lightDays.OrderBy(x => _random.Next()))
                    {
                        if (!scheduleGrid.ContainsKey(lesson.Class) || !scheduleGrid[lesson.Class].ContainsKey(day))
                            continue;

                        bool subjectAlreadyInDay = schedule.Lessons.Any(l =>
                            l.Class == lesson.Class &&
                            l.DayOfWeek == day &&
                            l.Subject == lesson.Subject);

                        if (subjectAlreadyInDay)
                            continue;

                        for (int num = maxPerDay; num >= 1; num--)
                        {
                            if (!scheduleGrid[lesson.Class][day].Contains(num))
                            {
                                AssignLessonWithResources(lesson, day, num);
                                assigned = true;
                                break;
                            }
                        }

                        if (assigned) break;
                    }

                    if (!assigned)
                    {
                        warnings.Add($"Не удалось назначить легкий урок {lesson.Class} - {lesson.Subject}");
                    }
                }

                allLessons = allLessons.Where(l => !l.IsAssigned).ToList();

                // ============================================================
                // 4. СРЕДНИЕ ПРЕДМЕТЫ
                // ============================================================
                var mediumLessons = allLessons
                    .Where(l => l.DayCategory == "medium" && !l.IsAssigned)
                    .ToList();

                foreach (var lesson in mediumLessons)
                {
                    if (!classMaxLessons.ContainsKey(lesson.Class)) continue;
                    int maxPerDay = classMaxLessons[lesson.Class];
                    bool assigned = false;

                    foreach (var day in availableDays.OrderBy(x => _random.Next()))
                    {
                        if (!scheduleGrid.ContainsKey(lesson.Class) || !scheduleGrid[lesson.Class].ContainsKey(day))
                            continue;

                        bool subjectAlreadyInDay = schedule.Lessons.Any(l =>
                            l.Class == lesson.Class &&
                            l.DayOfWeek == day &&
                            l.Subject == lesson.Subject);

                        if (subjectAlreadyInDay)
                            continue;

                        int usedSlots = scheduleGrid[lesson.Class][day].Count;
                        if (usedSlots >= maxPerDay)
                            continue;

                        int? freeSlot = FindFreeSlot(lesson.Class, day, scheduleGrid, maxPerDay);
                        if (freeSlot.HasValue)
                        {
                            AssignLessonWithResources(lesson, day, freeSlot.Value);
                            assigned = true;
                            break;
                        }
                    }

                    if (!assigned)
                    {
                        warnings.Add($"Не удалось назначить средний урок {lesson.Class} - {lesson.Subject}");
                    }
                }

                allLessons = allLessons.Where(l => !l.IsAssigned).ToList();

                // ============================================================
                // 5. ОСТАВШИЕСЯ УРОКИ
                // ============================================================
                if (allLessons.Any())
                {
                    foreach (var lesson in allLessons)
                    {
                        if (!classMaxLessons.ContainsKey(lesson.Class)) continue;
                        int maxPerDay = classMaxLessons[lesson.Class];
                        bool assigned = false;

                        foreach (var day in availableDays.OrderBy(x => _random.Next()))
                        {
                            if (!scheduleGrid.ContainsKey(lesson.Class) || !scheduleGrid[lesson.Class].ContainsKey(day))
                                continue;

                            bool subjectAlreadyInDay = schedule.Lessons.Any(l =>
                                l.Class == lesson.Class &&
                                l.DayOfWeek == day &&
                                l.Subject == lesson.Subject);

                            if (subjectAlreadyInDay)
                                continue;

                            int usedSlots = scheduleGrid[lesson.Class][day].Count;
                            if (usedSlots >= maxPerDay)
                                continue;

                            int? freeSlot = FindFreeSlot(lesson.Class, day, scheduleGrid, maxPerDay);
                            if (freeSlot.HasValue)
                            {
                                AssignLessonWithResources(lesson, day, freeSlot.Value);
                                assigned = true;
                                break;
                            }
                        }

                        if (!assigned)
                        {
                            warnings.Add($"Не удалось назначить урок {lesson.Class} - {lesson.Subject}");
                        }
                    }
                }

                // ============================================================
                // 6. ЗАПОЛНЯЕМ ПУСТЫЕ СЛОТЫ
                // ============================================================
                var availableSubjectsPerClass = new Dictionary<string, List<string>>();
                foreach (var className in validClasses)
                {
                    availableSubjectsPerClass[className] = classPlans[className].Keys.ToList();
                }

                foreach (var className in validClasses)
                {
                    if (!classMaxLessons.ContainsKey(className)) continue;
                    int maxPerDay = classMaxLessons[className];
                    var subjects = availableSubjectsPerClass[className];

                    foreach (var day in availableDays)
                    {
                        if (!scheduleGrid.ContainsKey(className) || !scheduleGrid[className].ContainsKey(day))
                            continue;

                        int usedSlots = scheduleGrid[className][day].Count;
                        int emptySlots = maxPerDay - usedSlots;

                        if (emptySlots > 0)
                        {
                            for (int i = 0; i < emptySlots; i++)
                            {
                                string selectedSubject = null;
                                var subjectsNotInDay = subjects
                                    .Where(s => !schedule.Lessons.Any(l =>
                                        l.Class == className &&
                                        l.DayOfWeek == day &&
                                        l.Subject == s))
                                    .ToList();

                                if (subjectsNotInDay.Any())
                                {
                                    selectedSubject = subjectsNotInDay[_random.Next(subjectsNotInDay.Count)];
                                }
                                else
                                {
                                    selectedSubject = subjects[_random.Next(subjects.Count)];
                                }

                                int? freeSlot = FindFreeSlot(className, day, scheduleGrid, maxPerDay);
                                if (freeSlot.HasValue)
                                {
                                    int difficulty = 5;
                                    if (subjectDifficultyCache.ContainsKey(className) &&
                                        subjectDifficultyCache[className].ContainsKey(selectedSubject))
                                    {
                                        difficulty = subjectDifficultyCache[className][selectedSubject];
                                    }

                                    var fillerLesson = new LessonRequest
                                    {
                                        Class = className,
                                        ClassGrade = classGrades[className],
                                        Subject = selectedSubject,
                                        Difficulty = difficulty,
                                        HoursPerWeek = 1,
                                        IsAssigned = false,
                                        IsFiller = true,
                                        DayCategory = GetSubjectCategory(difficulty),
                                        IsMondayOnly = false
                                    };

                                    AssignLessonWithResources(fillerLesson, day, freeSlot.Value);
                                    warnings.Add($"ℹ️ {className} {day} {freeSlot.Value}: добавлен {selectedSubject} (заполнитель)");
                                }
                            }
                        }
                    }
                }

                // ============================================================
                // 7. ФИНАЛЬНАЯ СОРТИРОВКА
                // ============================================================
                schedule.Lessons = schedule.Lessons
                    .OrderBy(l => Array.IndexOf(allDays.ToArray(), l.DayOfWeek))
                    .ThenBy(l => l.LessonNumber)
                    .ThenBy(l => l.Class)
                    .ToList();

                // ============================================================
                // 8. СТАТИСТИКА
                // ============================================================
                result.Statistics = new Dictionary<string, int>
                {
                    ["Всего уроков"] = schedule.Lessons.Count,
                    ["Классов"] = schedule.Lessons.Select(l => l.Class).Distinct().Count(),
                    ["Учителей"] = schedule.Lessons.Select(l => l.Teacher).Distinct().Count(),
                    ["Предметов"] = schedule.Lessons.Select(l => l.Subject).Distinct().Count(),
                    ["Дней недели"] = availableDays.Count
                };

                var dayStats = new Dictionary<string, int>();
                foreach (var day in availableDays)
                {
                    dayStats[day] = schedule.Lessons.Count(l => l.DayOfWeek == day);
                }
                result.Statistics["Уроков в понедельник"] = dayStats.ContainsKey("Понедельник") ? dayStats["Понедельник"] : 0;
                result.Statistics["Уроков во вторник"] = dayStats.ContainsKey("Вторник") ? dayStats["Вторник"] : 0;
                result.Statistics["Уроков в среду"] = dayStats.ContainsKey("Среда") ? dayStats["Среда"] : 0;
                result.Statistics["Уроков в четверг"] = dayStats.ContainsKey("Четверг") ? dayStats["Четверг"] : 0;
                result.Statistics["Уроков в пятницу"] = dayStats.ContainsKey("Пятница") ? dayStats["Пятница"] : 0;

                foreach (var className in validClasses)
                {
                    int actual = schedule.Lessons.Count(l => l.Class == className);
                    result.ClassLoad[className] = actual;

                    int maxPerWeek = GetMaxLessonsPerWeek(classGrades[className]);
                    if (actual < maxPerWeek)
                    {
                        warnings.Add($"⚠️ {className}: {actual} из {maxPerWeek} уроков (не хватает {maxPerWeek - actual})");
                    }
                }

                result.TeacherWorkload = teacherWorkload;
                result.SubjectDistribution = subjectDistribution;
                result.Warnings = warnings;
                result.Success = true;
                result.Message = $"Расписание успешно сгенерировано. Всего уроков: {schedule.Lessons.Count}";
                result.GeneratedSchedule = schedule;

                System.Diagnostics.Debug.WriteLine($"=== ГЕНЕРАЦИЯ ЗАВЕРШЕНА: {schedule.Lessons.Count} уроков ===");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка генерации: {ex.Message}");
                result.Success = false;
                result.Message = $"Ошибка генерации: {ex.Message}";
                return result;
            }
        }

        // ============================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ============================================================

        private int GetMaxLessonsPerDay(int grade)
        {
            if (grade >= 5 && grade <= 6) return 6;
            if (grade >= 7 && grade <= 9) return 7;
            if (grade >= 10 && grade <= 11) return 8;
            return 7;
        }

        private int GetMaxLessonsPerWeek(int grade)
        {
            if (grade >= 5 && grade <= 6) return 30;
            if (grade >= 7 && grade <= 9) return 35;
            if (grade >= 10 && grade <= 11) return 40;
            return 35;
        }

        private string GetDayCategory(string day)
        {
            if (day == "Понедельник" || day == "Пятница")
                return "light";
            else
                return "heavy";
        }

        private string GetSubjectCategory(int difficulty)
        {
            if (difficulty <= 4) return "light";
            if (difficulty >= 7) return "heavy";
            return "medium";
        }

        private string CorrectSubjectName(string subjectName, int grade)
        {
            if (string.IsNullOrEmpty(subjectName)) return subjectName;

            if (grade >= 5 && grade <= 6)
            {
                if (subjectName == "Алгебра" || subjectName == "Геометрия" ||
                    subjectName == "Вероятность и статистика" || subjectName == "Математика")
                {
                    return "Математика";
                }
            }

            return subjectName;
        }

        private int GetSubjectDifficultyFromDB(string subjectName, int grade, Dictionary<string, SubjectDifficulty> difficultyDict)
        {
            if (difficultyDict.ContainsKey(subjectName))
            {
                var d = difficultyDict[subjectName];
                switch (grade)
                {
                    case 5: return d.Grade5;
                    case 6: return d.Grade6;
                    case 7: return d.Grade7;
                    case 8: return d.Grade8;
                    case 9: return d.Grade9;
                    case 10: return d.Grade10;
                    case 11: return d.Grade11;
                    default: return d.Grade5;
                }
            }

            string altName = subjectName;
            if (subjectName == "Математика" && grade >= 7)
            {
                altName = "Алгебра";
                if (difficultyDict.ContainsKey(altName))
                {
                    var d = difficultyDict[altName];
                    switch (grade)
                    {
                        case 7: return d.Grade7;
                        case 8: return d.Grade8;
                        case 9: return d.Grade9;
                        case 10: return d.Grade10;
                        case 11: return d.Grade11;
                        default: return d.Grade7;
                    }
                }
            }

            return 5;
        }

        private List<TeacherInfo> ConvertToGeneratorTeachers(List<DbHelper.TeacherInfo> dbTeachers, DbHelper dbHelper)
        {
            if (dbTeachers == null) return new List<TeacherInfo>();

            var result = new List<TeacherInfo>();

            foreach (var t in dbTeachers)
            {
                var teacher = new TeacherInfo
                {
                    Id = t.Id,
                    Name = t.FullName,
                    Subject = t.Subject,
                    MaxHoursPerWeek = t.MaxHours > 0 ? t.MaxHours : 20,
                    Room = t.Room,
                    SubjectsByClass = new Dictionary<string, List<string>>(),
                    SubjectClassCache = new Dictionary<string, HashSet<string>>(),
                    DaysOff = new HashSet<string>()
                };

                try
                {
                    var subjectsByClass = dbHelper.GetTeacherSubjectsByClass(t.Id);
                    teacher.SubjectsByClass = subjectsByClass;
                    teacher.Classes = subjectsByClass.Keys.ToList();

                    foreach (var kvp in subjectsByClass)
                    {
                        string className = kvp.Key;
                        foreach (string subject in kvp.Value)
                        {
                            if (!teacher.SubjectClassCache.ContainsKey(subject))
                                teacher.SubjectClassCache[subject] = new HashSet<string>();
                            teacher.SubjectClassCache[subject].Add(className);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки предметов для {t.FullName}: {ex.Message}");
                }

                try
                {
                    var daysOff = dbHelper.GetTeacherDaysOff(t.Id);
                    foreach (var day in daysOff)
                    {
                        string russianDay;
                        switch (day)
                        {
                            case "Monday":
                                russianDay = "Понедельник";
                                break;
                            case "Tuesday":
                                russianDay = "Вторник";
                                break;
                            case "Wednesday":
                                russianDay = "Среда";
                                break;
                            case "Thursday":
                                russianDay = "Четверг";
                                break;
                            case "Friday":
                                russianDay = "Пятница";
                                break;
                            default:
                                russianDay = day;
                                break;
                        }
                        teacher.DaysOff.Add(russianDay);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки выходных для {t.FullName}: {ex.Message}");
                }

                result.Add(teacher);
            }

            return result;
        }

        private SubjectDifficulty ConvertToGeneratorDifficulty(DbHelper.SubjectDifficulty dbDifficulty)
        {
            return new SubjectDifficulty
            {
                Id = dbDifficulty.Id,
                SubjectName = dbDifficulty.SubjectName,
                Grade5 = dbDifficulty.Grade5,
                Grade6 = dbDifficulty.Grade6,
                Grade7 = dbDifficulty.Grade7,
                Grade8 = dbDifficulty.Grade8,
                Grade9 = dbDifficulty.Grade9,
                Grade10 = dbDifficulty.Grade10,
                Grade11 = dbDifficulty.Grade11
            };
        }

        private int GetGradeFromClassName(string className)
        {
            if (className.StartsWith("5")) return 5;
            if (className.StartsWith("6")) return 6;
            if (className.StartsWith("7")) return 7;
            if (className.StartsWith("8")) return 8;
            if (className.StartsWith("9")) return 9;
            if (className.StartsWith("10")) return 10;
            if (className.StartsWith("11")) return 11;
            return 5;
        }

        private TimeSpan CalculateStartTime(int lessonNumber, int startHour, int startMinute, int duration, int breakDuration)
        {
            int totalMinutes = (lessonNumber - 1) * (duration + breakDuration);
            return new TimeSpan(startHour, startMinute + totalMinutes, 0);
        }

        private TimeSpan CalculateEndTime(int lessonNumber, int startHour, int startMinute, int duration, int breakDuration)
        {
            int totalMinutes = (lessonNumber - 1) * (duration + breakDuration) + duration;
            return new TimeSpan(startHour, startMinute + totalMinutes, 0);
        }

        private string GenerateRoomNumber()
        {
            return $"Каб.{_random.Next(101, 415)}";
        }

        private int? FindFreeSlot(string className, string day,
            Dictionary<string, Dictionary<string, HashSet<int>>> scheduleGrid, int maxLessons)
        {
            if (!scheduleGrid.ContainsKey(className) || !scheduleGrid[className].ContainsKey(day))
                return null;

            var usedSlots = scheduleGrid[className][day];

            for (int i = 1; i <= maxLessons; i++)
            {
                if (!usedSlots.Contains(i))
                    return i;
            }

            return null;
        }

        private TeacherInfo FindBestTeacher(
            string subject,
            string className,
            int classGrade,
            Dictionary<string, int> workload,
            List<TeacherInfo> allTeachers,
            Dictionary<string, List<TeacherInfo>> teachersBySubject,
            string dayOfWeek,
            bool respectDaysOff)
        {
            if (!teachersBySubject.ContainsKey(subject))
            {
                return null;
            }

            var availableTeachers = teachersBySubject[subject]
                .Where(t =>
                    t.HasSubjectInClass(subject, className) &&
                    t.MaxHoursPerWeek > (workload.ContainsKey(t.Name) ? workload[t.Name] : 0))
                .ToList();

            if (respectDaysOff)
            {
                availableTeachers = availableTeachers
                    .Where(t => t.IsWorking(dayOfWeek))
                    .ToList();
            }

            return availableTeachers
                .OrderBy(t => workload.ContainsKey(t.Name) ? workload[t.Name] : 0)
                .ThenBy(t => t.Id)
                .FirstOrDefault();
        }
    }
}