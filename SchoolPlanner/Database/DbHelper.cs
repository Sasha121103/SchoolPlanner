using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using static SchoolPlanner.Database.Attendance;

namespace SchoolPlanner.Database
{
    public class DbHelper
    {
        private string connectionString = "Server=127.0.0.1;port=3306;Database=schoolplanner;Uid=root;Pwd=root;Charset=utf8;";

        public DbHelper()
        {
            InitializeDatabase();

        }

        private void InitializeDatabase()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Просто проверяем соединение
                    string testQuery = "SELECT COUNT(*) FROM users";
                    using (MySqlCommand cmd = new MySqlCommand(testQuery, conn))
                    {
                        cmd.ExecuteScalar();
                    }

                    // Нормализуем предметы (удаляем дубли)
                    try
                    {
                        NormalizeSubjects();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка нормализации предметов: {ex.Message}");
                        // Не критично, продолжаем работу
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к БД: {ex.Message}\n\n" +
                    "Убедитесь что:\n" +
                    "1. MySQL Server запущен\n" +
                    "2. База данных SchoolPlanner создана\n" +
                    "3. Параметры подключения верны",
                    "Ошибка БД",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        // ============================================================
        // КЛАССЫ ДЛЯ РАБОТЫ
        // ============================================================

        public class StudyPlan
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Variant { get; set; }
            public string Description { get; set; }
            public bool IsActive { get; set; }
            public int CreatedBy { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public List<PlanSubject> Subjects { get; set; } = new List<PlanSubject>();
        }

        public class PlanSubject
        {
            public int Id { get; set; }
            public int PlanId { get; set; }
            public string SubjectName { get; set; }
            public int? SubjectId { get; set; }
            public int Grade { get; set; }
            public int HoursPerWeek { get; set; }
            public int Difficulty { get; set; }
            public bool IsRequired { get; set; }
            public int SortOrder { get; set; }
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

        public class TeacherInfo
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string FullName { get; set; }
            public string Username { get; set; }
            public string Subject { get; set; }
            public string SubjectsList { get; set; }
            public int MaxHours { get; set; }
            public string Room { get; set; }  // Основной кабинет
            public List<TeacherRoom> Rooms { get; set; } = new List<TeacherRoom>(); // Все закрепленные кабинеты
            public string Qualification { get; set; }
            public int ExperienceYears { get; set; }
            public string Phone { get; set; }
            public string Email { get; set; }
        }

        public class TeacherRoom
        {
            public int Id { get; set; }
            public int TeacherId { get; set; }
            public int RoomId { get; set; }
            public bool IsPrimary { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }

            // Дополнительные поля для отображения
            public string TeacherName { get; set; }
            public string RoomNumber { get; set; }
        }
        public class SubjectInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int HoursPerWeek { get; set; }
            public int? SortOrder { get; set; } // Добавлено
        }
        public class RoomInfo
        {
            public int Id { get; set; }
            public string Number { get; set; }
            public string Subject { get; set; }
            public string TeacherName { get; set; }
            public int? PrimaryTeacherId { get; set; }
            public List<TeacherRoom> Teachers { get; set; } = new List<TeacherRoom>(); // Все учителя в кабинете
            public bool IsActive { get; set; }
        }

        // ============================================================
        // АУТЕНТИФИКАЦИЯ
        // ============================================================

        public User AuthenticateUser(string username, string password)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Используйте @username и @password
                    string query = @"SELECT id, full_name, username, role, subject, max_hours_per_week 
                           FROM users 
                           WHERE username = @username 
                           AND password_hash = @password";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@password", password);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var user = new User
                                {
                                    Id = reader.GetInt32("id"),
                                    FullName = reader.GetString("full_name"),
                                    Username = reader.GetString("username"),
                                    Role = (UserRole)Enum.Parse(typeof(UserRole), reader.GetString("role")),
                                    Subject = reader.IsDBNull(reader.GetOrdinal("subject")) ? null : reader.GetString("subject"),
                                    MaxHoursPerWeek = reader.IsDBNull(reader.GetOrdinal("max_hours_per_week")) ? 20 : reader.GetInt32("max_hours_per_week")
                                };

                                System.Diagnostics.Debug.WriteLine($"✅ Аутентификация успешна: ID={user.Id}, Name={user.FullName}");
                                return user;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ Пользователь не найден: {username}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка аутентификации: {ex.Message}");
                MessageBox.Show($"Ошибка подключения к БД: {ex.Message}");
            }
            return null;
        }
        // ============================================================
        // КЛАССЫ
        // ============================================================

        public List<SchoolClass> GetAllClasses()
        {
            List<SchoolClass> classes = new List<SchoolClass>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT id, name, students_count FROM classes ORDER BY name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                classes.Add(new SchoolClass
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("name"),
                                    StudentsCount = reader.IsDBNull(reader.GetOrdinal("students_count")) ? 0 : reader.GetInt32("students_count")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки классов: {ex.Message}");
            }

            return classes;
        }

        public int AddClass(string className, int grade)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        INSERT INTO classes (name, grade_level, students_count)
                        VALUES (@name, @grade, 0);
                        SELECT LAST_INSERT_ID();";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", className);
                        cmd.Parameters.AddWithValue("@grade", grade);

                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка добавления класса: {ex.Message}");
                return -1;
            }
        }

        public bool UpdateClass(int classId, string className, int grade)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        UPDATE classes 
                        SET name = @name,
                            grade_level = @grade
                        WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", classId);
                        cmd.Parameters.AddWithValue("@name", className);
                        cmd.Parameters.AddWithValue("@grade", grade);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления класса: {ex.Message}");
                return false;
            }
        }

        public bool DeleteClass(int classId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string checkStudentsQuery = "SELECT COUNT(*) FROM students WHERE class_id = @classId AND is_active = TRUE";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkStudentsQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@classId", classId);
                        int studentCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (studentCount > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"Нельзя удалить класс {classId}: есть {studentCount} учеников");
                            return false;
                        }
                    }

                    string checkScheduleQuery = @"
                        SELECT COUNT(*) FROM schedule_lessons 
                        WHERE class_id = @classId";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkScheduleQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@classId", classId);
                        int scheduleCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (scheduleCount > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"Нельзя удалить класс {classId}: есть {scheduleCount} уроков в расписании");
                            return false;
                        }
                    }

                    string getClassNameQuery = "SELECT name FROM classes WHERE id = @id";
                    string className = "";
                    using (MySqlCommand cmd = new MySqlCommand(getClassNameQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", classId);
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                            className = result.ToString();
                    }

                    if (!string.IsNullOrEmpty(className))
                    {
                        string deletePlanQuery = "DELETE FROM class_plan WHERE class_name = @className";
                        using (MySqlCommand cmd = new MySqlCommand(deletePlanQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@className", className);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    string deleteQuery = "DELETE FROM classes WHERE id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", classId);
                        int result = cmd.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления класса: {ex.Message}");
                return false;
            }
        }

        public bool HardDeleteClass(int classId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string getClassNameQuery = "SELECT name FROM classes WHERE id = @id";
                            string className = "";
                            using (MySqlCommand cmd = new MySqlCommand(getClassNameQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", classId);
                                var result = cmd.ExecuteScalar();
                                if (result != null)
                                    className = result.ToString();
                            }

                            if (!string.IsNullOrEmpty(className))
                            {
                                string deleteStudentsQuery = "DELETE FROM students WHERE class_id = @classId";
                                using (MySqlCommand cmd = new MySqlCommand(deleteStudentsQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@classId", classId);
                                    cmd.ExecuteNonQuery();
                                }

                                string deletePlanQuery = "DELETE FROM class_plan WHERE class_name = @className";
                                using (MySqlCommand cmd = new MySqlCommand(deletePlanQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@className", className);
                                    cmd.ExecuteNonQuery();
                                }

                                string deleteLessonsQuery = "DELETE FROM schedule_lessons WHERE class_name = @className";
                                using (MySqlCommand cmd = new MySqlCommand(deleteLessonsQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@className", className);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            string deleteClassQuery = "DELETE FROM classes WHERE id = @id";
                            using (MySqlCommand cmd = new MySqlCommand(deleteClassQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", classId);
                                int result = cmd.ExecuteNonQuery();

                                transaction.Commit();
                                return result > 0;
                            }
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка полного удаления класса: {ex.Message}");
                return false;
            }
        }

        public int AddMultipleClasses(List<string> classNames, int startGrade)
        {
            int added = 0;

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    foreach (string className in classNames)
                    {
                        int grade = startGrade;
                        if (className.StartsWith("5")) grade = 5;
                        else if (className.StartsWith("6")) grade = 6;
                        else if (className.StartsWith("7")) grade = 7;
                        else if (className.StartsWith("8")) grade = 8;
                        else if (className.StartsWith("9")) grade = 9;
                        else if (className.StartsWith("10")) grade = 10;
                        else if (className.StartsWith("11")) grade = 11;

                        string query = @"
                            INSERT INTO classes (name, grade_level, students_count)
                            VALUES (@name, @grade, 0)
                            ON DUPLICATE KEY UPDATE id = id";

                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@name", className);
                            cmd.Parameters.AddWithValue("@grade", grade);

                            if (cmd.ExecuteNonQuery() > 0)
                                added++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка добавления нескольких классов: {ex.Message}");
            }

            return added;
        }

        // ============================================================
        // УЧИТЕЛЯ
        // ============================================================

        public List<Teacher> GetAllTeachers()
        {
            List<Teacher> teachers = new List<Teacher>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // ✅ ТОЛЬКО ИЗ USERS, БЕЗ TEACHERS
                    string query = @"
                SELECT 
                    id as user_id, 
                    full_name,
                    subject,
                    subjects_list,
                    max_hours_per_week,
                    room
                FROM users 
                WHERE role = 'Teacher' 
                ORDER BY full_name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string subjectValue = "";
                                if (!reader.IsDBNull(reader.GetOrdinal("subjects_list")))
                                {
                                    subjectValue = reader.GetString("subjects_list");
                                }
                                else if (!reader.IsDBNull(reader.GetOrdinal("subject")))
                                {
                                    subjectValue = reader.GetString("subject");
                                }

                                teachers.Add(new Teacher
                                {
                                    UserId = reader.GetInt32("user_id"),
                                    FullName = reader.GetString("full_name"),
                                    Name = reader.GetString("full_name"),
                                    Subject = subjectValue,
                                    MaxHoursPerWeek = reader.IsDBNull(reader.GetOrdinal("max_hours_per_week")) ? 20 : reader.GetInt32("max_hours_per_week"),
                                    Room = reader.IsDBNull(reader.GetOrdinal("room")) ? "" : reader.GetString("room")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetAllTeachers: {ex.Message}");
            }

            return teachers;
        }
        /// <summary>
        /// Удаляет учителя по ID пользователя
        /// </summary>
        public bool DeleteTeacher(int userId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== DeleteTeacher START, UserId: {userId} ===");

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Проверяем, существует ли пользователь
                            string checkUserQuery = "SELECT COUNT(*) FROM users WHERE id = @userId AND role = 'Teacher'";
                            int userExists = 0;
                            using (MySqlCommand cmd = new MySqlCommand(checkUserQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@userId", userId);
                                userExists = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            if (userExists == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Пользователь с ID {userId} не найден или не является учителем");
                                return false;
                            }

                            // 2. Обновляем schedule_lessons (назначаем администратора)
                            string updateLessonsQuery = @"
                        UPDATE schedule_lessons 
                        SET teacher_id = 1,
                            teacher_name = 'Администратор'
                        WHERE teacher_id = @userId";

                            using (MySqlCommand cmd = new MySqlCommand(updateLessonsQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@userId", userId);
                                int updated = cmd.ExecuteNonQuery();
                                System.Diagnostics.Debug.WriteLine($"Обновлено уроков: {updated}");
                            }

                            // 3. Обновляем lesson_plans (назначаем администратора)
                            string updatePlansQuery = @"
                        UPDATE lesson_plans 
                        SET teacher_id = 1,
                            teacher_name = 'Администратор'
                        WHERE teacher_id = @userId";

                            using (MySqlCommand cmd = new MySqlCommand(updatePlansQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@userId", userId);
                                int updated = cmd.ExecuteNonQuery();
                                System.Diagnostics.Debug.WriteLine($"Обновлено планов уроков: {updated}");
                            }

                            // 4. Удаляем пользователя (каскадно удалятся все связанные данные)
                            string deleteQuery = "DELETE FROM users WHERE id = @userId";
                            using (MySqlCommand cmd = new MySqlCommand(deleteQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@userId", userId);
                                int result = cmd.ExecuteNonQuery();
                                System.Diagnostics.Debug.WriteLine($"Удалено из users: {result}");
                            }

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"=== DeleteTeacher SUCCESS ===");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"Ошибка в транзакции: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка DeleteTeacher: {ex.Message}");
                MessageBox.Show($"Ошибка удаления учителя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        /// <summary>
        /// Проверяет, можно ли удалить учителя
        /// </summary>
        public bool CanDeleteTeacher(int userId, out string reason)
        {
            reason = "";

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, существует ли пользователь
                    string checkUserQuery = "SELECT COUNT(*) FROM users WHERE id = @userId AND role = 'Teacher'";
                    using (MySqlCommand cmd = new MySqlCommand(checkUserQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        if (count == 0)
                        {
                            reason = "Учитель не найден";
                            return false;
                        }
                    }

                    // Проверяем уроки в расписании
                    string checkLessonsQuery = "SELECT COUNT(*) FROM schedule_lessons WHERE teacher_id = @userId";
                    using (MySqlCommand cmd = new MySqlCommand(checkLessonsQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        if (count > 0)
                        {
                            reason = $"Учитель назначен на {count} уроков в расписании. Уроки будут переназначены на администратора.";
                            return true;
                        }
                    }

                    // Проверяем планы уроков
                    string checkPlansQuery = "SELECT COUNT(*) FROM lesson_plans WHERE teacher_id = @userId";
                    using (MySqlCommand cmd = new MySqlCommand(checkPlansQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        if (count > 0)
                        {
                            reason = $"Учитель является автором {count} планов уроков. Планы будут переназначены на администратора.";
                            return true;
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка CanDeleteTeacher: {ex.Message}");
                reason = $"Ошибка проверки: {ex.Message}";
                return false;
            }
        }
        // ============================================================
        // ПРЕДМЕТЫ
        // ============================================================

        public List<SubjectInfo> GetAllSubjectsWithHours()
        {
            List<SubjectInfo> subjects = new List<SubjectInfo>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT id, name, hours_per_week, sort_order FROM subjects WHERE is_active = TRUE ORDER BY sort_order, name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                subjects.Add(new SubjectInfo
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("name"),
                                    HoursPerWeek = reader.GetInt32("hours_per_week"),
                                    SortOrder = reader.IsDBNull(reader.GetOrdinal("sort_order")) ? (int?)null : reader.GetInt32("sort_order")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения предметов: {ex.Message}");
            }

            return subjects;
        }

        public List<Subject> GetAllSubjects()
        {
            List<Subject> subjects = new List<Subject>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT id, name, duration_minutes FROM subjects WHERE is_active = TRUE ORDER BY name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                subjects.Add(new Subject
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("name"),
                                    DurationMinutes = reader.GetInt32("duration_minutes")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки предметов: {ex.Message}");
            }

            return subjects;
        }

        public int AddSubject(string name, int sortOrder)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Сначала проверяем, существует ли уже такой предмет
                    string checkQuery = "SELECT id FROM subjects WHERE name = @name AND is_active = TRUE";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@name", name);
                        var existing = checkCmd.ExecuteScalar();
                        if (existing != null && existing != DBNull.Value)
                        {
                            // Предмет уже существует
                            return Convert.ToInt32(existing);
                        }
                    }

                    // Добавляем предмет
                    string query = @"
                INSERT INTO subjects (name, hours_per_week, sort_order, is_active)
                VALUES (@name, 0, @sortOrder, TRUE);
                SELECT LAST_INSERT_ID();";

                    int subjectId;
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@sortOrder", sortOrder);
                        subjectId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Автоматически добавляем в шкалу трудности
                    AddSubjectToDifficulty(name);

                    return subjectId;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка добавления предмета: {ex.Message}");
                return -1;
            }
        }
        public bool UpdateSubject(int subjectId, string name, int hoursPerWeek)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        UPDATE subjects 
                        SET name = @name,
                            hours_per_week = @hours
                        WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", subjectId);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@hours", hoursPerWeek);

                        int result = cmd.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления предмета: {ex.Message}");
                return false;
            }
        }

        public bool DeleteSubject(int subjectId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string checkQuery = "SELECT COUNT(*) FROM schedule_lessons WHERE subject_id = @subjectId";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@subjectId", subjectId);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            string updateQuery = "UPDATE subjects SET is_active = FALSE WHERE id = @id";
                            using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", subjectId);
                                return cmd.ExecuteNonQuery() > 0;
                            }
                        }
                        else
                        {
                            string deleteQuery = "DELETE FROM subjects WHERE id = @id";
                            using (MySqlCommand cmd = new MySqlCommand(deleteQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", subjectId);
                                return cmd.ExecuteNonQuery() > 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления предмета: {ex.Message}");
                return false;
            }
        }

        // ============================================================
        // КАБИНЕТЫ
        // ============================================================

        public List<RoomInfo> GetAllRooms()
        {
            List<RoomInfo> rooms = new List<RoomInfo>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT id, number, subject, teacher_name FROM rooms ORDER BY number";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                rooms.Add(new RoomInfo
                                {
                                    Id = reader.GetInt32("id"),
                                    Number = reader.GetString("number"),
                                    Subject = reader.IsDBNull(reader.GetOrdinal("subject")) ? "" : reader.GetString("subject"),
                                    TeacherName = reader.IsDBNull(reader.GetOrdinal("teacher_name")) ? "" : reader.GetString("teacher_name")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения кабинетов: {ex.Message}");
            }

            return rooms;
        }

        // Добавление кабинета (возвращает int - ID записи)
        public int AddRoom(string number, string subject, string teacherName)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                INSERT INTO rooms (number, subject, teacher_name)
                VALUES (@number, @subject, @teacherName);
                SELECT LAST_INSERT_ID();";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@number", number);
                        cmd.Parameters.AddWithValue("@subject", subject ?? "");
                        cmd.Parameters.AddWithValue("@teacherName", teacherName ?? "");
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка AddRoom: {ex.Message}");
                return -1;
            }
        }

        // Обновление кабинета (возвращает bool)
        public bool UpdateRoom(int roomId, string number, string subject, string teacherName)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                UPDATE rooms 
                SET number = @number,
                    subject = @subject,
                    teacher_name = @teacherName
                WHERE id = @roomId";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@roomId", roomId);
                        cmd.Parameters.AddWithValue("@number", number);
                        cmd.Parameters.AddWithValue("@subject", subject ?? "");
                        cmd.Parameters.AddWithValue("@teacherName", teacherName ?? "");
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateRoom: {ex.Message}");
                return false;
            }
        }




        public bool DeleteRoom(int roomId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string checkQuery = "SELECT COUNT(*) FROM schedule_lessons WHERE room = (SELECT number FROM rooms WHERE id = @id)";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@id", roomId);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            MessageBox.Show("Кабинет используется в расписании. Сначала удалите уроки в этом кабинете.",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }
                    }

                    string deleteQuery = "DELETE FROM rooms WHERE id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", roomId);
                        int result = cmd.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления кабинета: {ex.Message}");
                return false;
            }
        }

        // ============================================================
        // ШКАЛА ТРУДНОСТИ
        // ============================================================

        public List<SubjectDifficulty> GetAllSubjectDifficulties()
        {
            List<SubjectDifficulty> difficulties = new List<SubjectDifficulty>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT * FROM subject_difficulty ORDER BY subject_name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                difficulties.Add(new SubjectDifficulty
                                {
                                    Id = reader.GetInt32("id"),
                                    SubjectName = reader.GetString("subject_name"),
                                    Grade5 = reader.GetInt32("grade_5"),
                                    Grade6 = reader.GetInt32("grade_6"),
                                    Grade7 = reader.GetInt32("grade_7"),
                                    Grade8 = reader.GetInt32("grade_8"),
                                    Grade9 = reader.GetInt32("grade_9"),
                                    Grade10 = reader.GetInt32("grade_10"),
                                    Grade11 = reader.GetInt32("grade_11")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки шкалы трудности: {ex.Message}");
            }

            return difficulties;
        }

        public bool UpdateSubjectDifficulty(SubjectDifficulty difficulty)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        UPDATE subject_difficulty 
                        SET grade_5 = @grade5,
                            grade_6 = @grade6,
                            grade_7 = @grade7,
                            grade_8 = @grade8,
                            grade_9 = @grade9,
                            grade_10 = @grade10,
                            grade_11 = @grade11
                        WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", difficulty.Id);
                        cmd.Parameters.AddWithValue("@grade5", difficulty.Grade5);
                        cmd.Parameters.AddWithValue("@grade6", difficulty.Grade6);
                        cmd.Parameters.AddWithValue("@grade7", difficulty.Grade7);
                        cmd.Parameters.AddWithValue("@grade8", difficulty.Grade8);
                        cmd.Parameters.AddWithValue("@grade9", difficulty.Grade9);
                        cmd.Parameters.AddWithValue("@grade10", difficulty.Grade10);
                        cmd.Parameters.AddWithValue("@grade11", difficulty.Grade11);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления шкалы трудности: {ex.Message}");
                return false;
            }
        }

        // ============================================================
        // УЧЕБНЫЕ ПЛАНЫ
        // ============================================================


        public bool DeleteStudyPlan(int planId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== DeleteStudyPlan START, Plan ID: {planId} ===");

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, используется ли план
                    string checkQuery = "SELECT COUNT(*) FROM class_plan WHERE plan_id = @planId";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@planId", planId);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            // Если план используется, делаем мягкое удаление
                            string updateQuery = "UPDATE study_plans SET is_active = FALSE WHERE id = @id";
                            using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", planId);
                                bool result = cmd.ExecuteNonQuery() > 0;
                                System.Diagnostics.Debug.WriteLine($"Soft delete plan {planId}, result: {result}");
                                return result;
                            }
                        }
                        else
                        {
                            // Полное удаление
                            string deleteQuery = "DELETE FROM study_plans WHERE id = @id";
                            using (MySqlCommand cmd = new MySqlCommand(deleteQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", planId);
                                bool result = cmd.ExecuteNonQuery() > 0;
                                System.Diagnostics.Debug.WriteLine($"Hard delete plan {planId}, result: {result}");
                                return result;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления учебного плана: {ex.Message}");
                return false;
            }
        }

        // ОСТАВЬТЕ этот метод (в секции УЧЕБНЫЕ ПЛАНЫ)
        public bool AssignClassToPlan(string className, int planId, int grade)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, есть ли уже запись в class_plan
                    string checkQuery = "SELECT COUNT(*) FROM class_plan WHERE class_name = @className";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@className", className);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            string updateQuery = @"
                        UPDATE class_plan 
                        SET plan_id = @planId, 
                            grade = @grade, 
                            updated_at = NOW() 
                        WHERE class_name = @className";

                            using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@planId", planId);
                                cmd.Parameters.AddWithValue("@grade", grade);
                                cmd.Parameters.AddWithValue("@className", className);
                                return cmd.ExecuteNonQuery() > 0;
                            }
                        }
                        else
                        {
                            string insertQuery = @"
                        INSERT INTO class_plan (class_name, plan_id, grade)
                        VALUES (@className, @planId, @grade)";

                            using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@className", className);
                                cmd.Parameters.AddWithValue("@planId", planId);
                                cmd.Parameters.AddWithValue("@grade", grade);
                                return cmd.ExecuteNonQuery() > 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка привязки класса к плану: {ex.Message}");
                return false;
            }
        }

        public List<ClassInfo> GetAllClassesWithInfo()
        {
            List<ClassInfo> classes = new List<ClassInfo>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    c.id, 
                    c.name, 
                    c.grade_level as grade,
                    (SELECT COUNT(*) FROM students WHERE class_id = c.id AND is_active = TRUE) as student_count,
                    sp.name as plan_name,
                    cp.plan_id
                FROM classes c
                LEFT JOIN class_plan cp ON c.name = cp.class_name
                LEFT JOIN study_plans sp ON cp.plan_id = sp.id
                GROUP BY c.id, c.name, c.grade_level, sp.name, cp.plan_id
                ORDER BY c.name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                classes.Add(new ClassInfo
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("name"),
                                    Grade = reader.IsDBNull(reader.GetOrdinal("grade")) ? 0 : reader.GetInt32("grade"),
                                    StudentCount = reader.IsDBNull(reader.GetOrdinal("student_count")) ? 0 : reader.GetInt32("student_count"),
                                    PlanName = reader.IsDBNull(reader.GetOrdinal("plan_name")) ? "Не назначен" : reader.GetString("plan_name"),
                                    PlanId = reader.IsDBNull(reader.GetOrdinal("plan_id")) ? (int?)null : reader.GetInt32("plan_id")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки классов с информацией: {ex.Message}");
            }

            return classes;
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
        // ============================================================
        // ПЛАНЫ УРОКОВ
        // ============================================================

        public int SaveLessonPlan(LessonPlan plan)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            int subjectId = GetSubjectId(conn, plan.Subject);
                            int classId = GetClassId(conn, plan.Class);

                            string query = @"
                                INSERT INTO lesson_plans 
                                (title, subject_id, subject_name, class_id, class_name, goal, 
                                 teacher_id, teacher_name, created_date, status)
                                VALUES 
                                (@title, @subjectId, @subjectName, @classId, @className, @goal,
                                 @teacherId, @teacherName, @createdDate, @status);
                                SELECT LAST_INSERT_ID();";

                            int planId;
                            using (MySqlCommand cmd = new MySqlCommand(query, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@title", plan.Title);
                                cmd.Parameters.AddWithValue("@subjectId", subjectId);
                                cmd.Parameters.AddWithValue("@subjectName", plan.Subject);
                                cmd.Parameters.AddWithValue("@classId", classId);
                                cmd.Parameters.AddWithValue("@className", plan.Class);
                                cmd.Parameters.AddWithValue("@goal", plan.Goal);
                                cmd.Parameters.AddWithValue("@teacherId", plan.TeacherId);
                                cmd.Parameters.AddWithValue("@teacherName", plan.TeacherName);
                                cmd.Parameters.AddWithValue("@createdDate", plan.CreatedDate);
                                cmd.Parameters.AddWithValue("@status", plan.Status.ToString());
                                planId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            for (int i = 0; i < plan.Tasks.Count; i++)
                            {
                                string taskQuery = @"
                                    INSERT INTO lesson_tasks (lesson_plan_id, task_text, sort_order)
                                    VALUES (@planId, @task, @sortOrder)";

                                using (MySqlCommand cmd = new MySqlCommand(taskQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@planId", planId);
                                    cmd.Parameters.AddWithValue("@task", plan.Tasks[i]);
                                    cmd.Parameters.AddWithValue("@sortOrder", i);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            for (int i = 0; i < plan.Stages.Count; i++)
                            {
                                var stage = plan.Stages[i];
                                string stageQuery = @"
                                    INSERT INTO lesson_stages 
                                    (lesson_plan_id, name, duration, description, example, sort_order)
                                    VALUES 
                                    (@planId, @name, @duration, @description, @example, @sortOrder)";

                                using (MySqlCommand cmd = new MySqlCommand(stageQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@planId", planId);
                                    cmd.Parameters.AddWithValue("@name", stage.Name);
                                    cmd.Parameters.AddWithValue("@duration", stage.Duration);
                                    cmd.Parameters.AddWithValue("@description", stage.Description ?? "");
                                    cmd.Parameters.AddWithValue("@example", stage.Example ?? "");
                                    cmd.Parameters.AddWithValue("@sortOrder", i);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            return planId;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения плана: {ex.Message}");
                MessageBox.Show($"Ошибка сохранения плана: {ex.Message}");
                return -1;
            }
        }

        public List<LessonPlan> GetLessonPlansByTeacher(int teacherId)
        {
            List<LessonPlan> plans = new List<LessonPlan>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT * FROM lesson_plans 
                        WHERE teacher_id = @teacherId AND is_deleted = FALSE
                        ORDER BY created_date DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                plans.Add(MapToLessonPlan(reader));
                            }
                        }
                    }

                    foreach (var plan in plans)
                    {
                        LoadLessonDetails(plan);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки планов: {ex.Message}");
            }

            return plans;
        }

        public List<LessonPlan> GetAllLessonPlans()
        {
            List<LessonPlan> plans = new List<LessonPlan>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT * FROM lesson_plans 
                        WHERE is_deleted = FALSE 
                        ORDER BY created_date DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                plans.Add(MapToLessonPlan(reader));
                            }
                        }
                    }

                    foreach (var plan in plans)
                    {
                        LoadLessonDetails(plan);
                        LoadComments(plan);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки всех планов: {ex.Message}");
            }

            return plans;
        }

        public void UpdateLessonPlanStatus(int planId, LessonStatus status, int? reviewerId = null)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    if (reviewerId.HasValue && status == LessonStatus.Approved)
                    {
                        string query = @"
                            UPDATE lesson_plans 
                            SET status = @status, 
                                reviewed_by = @reviewerId, 
                                reviewed_date = NOW() 
                            WHERE id = @planId";

                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@status", status.ToString());
                            cmd.Parameters.AddWithValue("@reviewerId", reviewerId.Value);
                            cmd.Parameters.AddWithValue("@planId", planId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        string query = "UPDATE lesson_plans SET status = @status WHERE id = @planId";
                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@status", status.ToString());
                            cmd.Parameters.AddWithValue("@planId", planId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления статуса: {ex.Message}");
                MessageBox.Show($"Ошибка обновления статуса: {ex.Message}");
            }
        }

        public void AddComment(int? lessonPlanId, int? scheduleId, int userId, string userName, string text)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        INSERT INTO comments 
                        (lesson_plan_id, schedule_id, user_id, user_name, text, date)
                        VALUES 
                        (@lessonPlanId, @scheduleId, @userId, @userName, @text, NOW())";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@lessonPlanId", lessonPlanId.HasValue ? (object)lessonPlanId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@scheduleId", scheduleId.HasValue ? (object)scheduleId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@userName", userName);
                        cmd.Parameters.AddWithValue("@text", text);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка добавления комментария: {ex.Message}");
            }
        }

        // ============================================================
        // РАСПИСАНИЯ
        // ============================================================

        public List<Schedule> GetAllSchedules()
        {
            List<Schedule> schedules = new List<Schedule>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT id, name, start_date, end_date, status, created_by, created_at, approved_by, approved_date, is_active
                        FROM schedules 
                        WHERE is_active = TRUE 
                        ORDER BY start_date DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                schedules.Add(new Schedule
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("name"),
                                    StartDate = reader.GetDateTime("start_date"),
                                    EndDate = reader.GetDateTime("end_date"),
                                    Status = (ScheduleStatus)Enum.Parse(typeof(ScheduleStatus), reader.GetString("status")),
                                    CreatedBy = reader.GetInt32("created_by"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    ApprovedBy = reader.IsDBNull(reader.GetOrdinal("approved_by")) ? (int?)null : reader.GetInt32("approved_by"),
                                    ApprovedDate = reader.IsDBNull(reader.GetOrdinal("approved_date")) ? (DateTime?)null : reader.GetDateTime("approved_date"),
                                    IsActive = reader.GetBoolean("is_active")
                                });
                            }
                        }
                    }

                    foreach (var schedule in schedules)
                    {
                        LoadScheduleLessons(schedule);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки расписаний: {ex.Message}");
            }

            return schedules;
        }

        public int SaveSchedule(Schedule schedule)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string checkUserQuery = "SELECT COUNT(*) FROM users WHERE id = @userId";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkUserQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@userId", schedule.CreatedBy);
                        int userCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (userCount == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"Пользователь с ID {schedule.CreatedBy} не существует");

                            string findAdminQuery = "SELECT id FROM users WHERE role = 'Admin' LIMIT 1";
                            using (MySqlCommand findCmd = new MySqlCommand(findAdminQuery, conn))
                            {
                                var adminId = findCmd.ExecuteScalar();
                                if (adminId != null)
                                {
                                    schedule.CreatedBy = Convert.ToInt32(adminId);
                                    System.Diagnostics.Debug.WriteLine($"Используем администратора с ID {schedule.CreatedBy}");
                                }
                                else
                                {
                                    MessageBox.Show($"Ошибка: пользователь с ID {schedule.CreatedBy} не найден в базе данных");
                                    return -1;
                                }
                            }
                        }
                    }

                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string scheduleQuery = @"
                                INSERT INTO schedules 
                                (name, start_date, end_date, status, created_by, created_at, is_active)
                                VALUES 
                                (@name, @startDate, @endDate, @status, @createdBy, @createdAt, @isActive);
                                SELECT LAST_INSERT_ID();";

                            int scheduleId;
                            using (MySqlCommand cmd = new MySqlCommand(scheduleQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@name", schedule.Name);
                                cmd.Parameters.AddWithValue("@startDate", schedule.StartDate);
                                cmd.Parameters.AddWithValue("@endDate", schedule.EndDate);
                                cmd.Parameters.AddWithValue("@status", schedule.Status.ToString());
                                cmd.Parameters.AddWithValue("@createdBy", schedule.CreatedBy);
                                cmd.Parameters.AddWithValue("@createdAt", schedule.CreatedAt);
                                cmd.Parameters.AddWithValue("@isActive", schedule.IsActive);
                                scheduleId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            if (schedule.Lessons != null && schedule.Lessons.Any())
                            {
                                foreach (var lesson in schedule.Lessons)
                                {
                                    int subjectId = GetSubjectId(conn, lesson.Subject);
                                    int classId = GetClassId(conn, lesson.Class);
                                    int teacherId = GetTeacherIdByName(conn, lesson.Teacher);

                                    string lessonQuery = @"
                                        INSERT INTO schedule_lessons 
                                        (schedule_id, subject_id, subject, class_id, class_name, 
                                         teacher_id, teacher_name, day_of_week, lesson_number, 
                                         start_time, end_time, room, lesson_plan_id, 
                                         lesson_plan_title, homework, note, is_canceled)
                                        VALUES 
                                        (@scheduleId, @subjectId, @subject, @classId, @className,
                                         @teacherId, @teacherName, @dayOfWeek, @lessonNumber,
                                         @startTime, @endTime, @room, @lessonPlanId,
                                         @lessonPlanTitle, @homework, @note, @isCanceled)";

                                    using (MySqlCommand cmd = new MySqlCommand(lessonQuery, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@scheduleId", scheduleId);
                                        cmd.Parameters.AddWithValue("@subjectId", subjectId);
                                        cmd.Parameters.AddWithValue("@subject", lesson.Subject);
                                        cmd.Parameters.AddWithValue("@classId", classId);
                                        cmd.Parameters.AddWithValue("@className", lesson.Class);
                                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                                        cmd.Parameters.AddWithValue("@teacherName", lesson.Teacher);
                                        cmd.Parameters.AddWithValue("@dayOfWeek", lesson.DayOfWeek);
                                        cmd.Parameters.AddWithValue("@lessonNumber", lesson.LessonNumber);
                                        cmd.Parameters.AddWithValue("@startTime", lesson.StartTime);
                                        cmd.Parameters.AddWithValue("@endTime", lesson.EndTime);
                                        cmd.Parameters.AddWithValue("@room", lesson.Room ?? "");
                                        cmd.Parameters.AddWithValue("@lessonPlanId", lesson.LessonPlanId.HasValue ? (object)lesson.LessonPlanId.Value : DBNull.Value);
                                        cmd.Parameters.AddWithValue("@lessonPlanTitle", lesson.LessonPlanTitle ?? "");
                                        cmd.Parameters.AddWithValue("@homework", lesson.Homework ?? "");
                                        cmd.Parameters.AddWithValue("@note", lesson.Note ?? "");
                                        cmd.Parameters.AddWithValue("@isCanceled", lesson.IsCanceled);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            transaction.Commit();

                            System.Diagnostics.Debug.WriteLine($"Расписание сохранено с ID: {scheduleId}");
                            return scheduleId;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"Ошибка при вставке расписания: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения расписания: {ex.Message}");
                MessageBox.Show($"Ошибка сохранения расписания: {ex.Message}\n\nПроверьте, что пользователь с ID {schedule.CreatedBy} существует в таблице users.");
                return -1;
            }
        }

        public void UpdateSchedule(Schedule schedule)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string scheduleQuery = @"
                                UPDATE schedules 
                                SET name = @name,
                                    start_date = @startDate,
                                    end_date = @endDate,
                                    status = @status,
                                    approved_by = @approvedBy,
                                    approved_date = @approvedDate,
                                    updated_at = NOW(),
                                    is_active = @isActive
                                WHERE id = @id";

                            using (MySqlCommand cmd = new MySqlCommand(scheduleQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", schedule.Id);
                                cmd.Parameters.AddWithValue("@name", schedule.Name);
                                cmd.Parameters.AddWithValue("@startDate", schedule.StartDate);
                                cmd.Parameters.AddWithValue("@endDate", schedule.EndDate);
                                cmd.Parameters.AddWithValue("@status", schedule.Status.ToString());
                                cmd.Parameters.AddWithValue("@approvedBy", schedule.ApprovedBy.HasValue ? (object)schedule.ApprovedBy.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@approvedDate", schedule.ApprovedDate.HasValue ? (object)schedule.ApprovedDate.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@isActive", schedule.IsActive);
                                cmd.ExecuteNonQuery();
                            }

                            string deleteQuery = "DELETE FROM schedule_lessons WHERE schedule_id = @scheduleId";
                            using (MySqlCommand cmd = new MySqlCommand(deleteQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@scheduleId", schedule.Id);
                                cmd.ExecuteNonQuery();
                            }

                            if (schedule.Lessons != null && schedule.Lessons.Any())
                            {
                                foreach (var lesson in schedule.Lessons)
                                {
                                    int subjectId = GetSubjectId(conn, lesson.Subject);
                                    int classId = GetClassId(conn, lesson.Class);
                                    int teacherId = GetTeacherIdByName(conn, lesson.Teacher);

                                    string lessonQuery = @"
                                        INSERT INTO schedule_lessons 
                                        (schedule_id, subject_id, subject, class_id, class_name, 
                                         teacher_id, teacher_name, day_of_week, lesson_number, 
                                         start_time, end_time, room, lesson_plan_id, 
                                         lesson_plan_title, homework, note, is_canceled)
                                        VALUES 
                                        (@scheduleId, @subjectId, @subject, @classId, @className,
                                         @teacherId, @teacherName, @dayOfWeek, @lessonNumber,
                                         @startTime, @endTime, @room, @lessonPlanId,
                                         @lessonPlanTitle, @homework, @note, @isCanceled)";

                                    using (MySqlCommand cmd = new MySqlCommand(lessonQuery, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@scheduleId", schedule.Id);
                                        cmd.Parameters.AddWithValue("@subjectId", subjectId);
                                        cmd.Parameters.AddWithValue("@subject", lesson.Subject);
                                        cmd.Parameters.AddWithValue("@classId", classId);
                                        cmd.Parameters.AddWithValue("@className", lesson.Class);
                                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                                        cmd.Parameters.AddWithValue("@teacherName", lesson.Teacher);
                                        cmd.Parameters.AddWithValue("@dayOfWeek", lesson.DayOfWeek);
                                        cmd.Parameters.AddWithValue("@lessonNumber", lesson.LessonNumber);
                                        cmd.Parameters.AddWithValue("@startTime", lesson.StartTime);
                                        cmd.Parameters.AddWithValue("@endTime", lesson.EndTime);
                                        cmd.Parameters.AddWithValue("@room", lesson.Room ?? "");
                                        cmd.Parameters.AddWithValue("@lessonPlanId", lesson.LessonPlanId.HasValue ? (object)lesson.LessonPlanId.Value : DBNull.Value);
                                        cmd.Parameters.AddWithValue("@lessonPlanTitle", lesson.LessonPlanTitle ?? "");
                                        cmd.Parameters.AddWithValue("@homework", lesson.Homework ?? "");
                                        cmd.Parameters.AddWithValue("@note", lesson.Note ?? "");
                                        cmd.Parameters.AddWithValue("@isCanceled", lesson.IsCanceled);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления расписания: {ex.Message}");
                MessageBox.Show($"Ошибка обновления расписания: {ex.Message}");
            }
        }

        // При создании урока автоматически подставляем кабинет учителя
        public void AddScheduleLesson(ScheduleLesson lesson)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Если кабинет не указан, пытаемся получить автоматически
                    if (string.IsNullOrEmpty(lesson.Room))
                    {
                        lesson.Room = GetRoomForTeacher(lesson.TeacherId, lesson.Subject);
                    }

                    int subjectId = GetSubjectId(conn, lesson.Subject);
                    int classId = GetClassId(conn, lesson.Class);
                    int teacherId = GetTeacherIdByName(conn, lesson.Teacher);

                    string query = @"
                INSERT INTO schedule_lessons 
                (schedule_id, subject_id, subject, class_id, class_name, 
                 teacher_id, teacher_name, day_of_week, lesson_number, 
                 start_time, end_time, room, lesson_plan_id, 
                 lesson_plan_title, homework, note, is_canceled)
                VALUES 
                (@scheduleId, @subjectId, @subject, @classId, @className,
                 @teacherId, @teacherName, @dayOfWeek, @lessonNumber,
                 @startTime, @endTime, @room, @lessonPlanId,
                 @lessonPlanTitle, @homework, @note, @isCanceled);
                SELECT LAST_INSERT_ID();";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@scheduleId", lesson.ScheduleId);
                        cmd.Parameters.AddWithValue("@subjectId", subjectId);
                        cmd.Parameters.AddWithValue("@subject", lesson.Subject);
                        cmd.Parameters.AddWithValue("@classId", classId);
                        cmd.Parameters.AddWithValue("@className", lesson.Class);
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        cmd.Parameters.AddWithValue("@teacherName", lesson.Teacher);
                        cmd.Parameters.AddWithValue("@dayOfWeek", lesson.DayOfWeek);
                        cmd.Parameters.AddWithValue("@lessonNumber", lesson.LessonNumber);
                        cmd.Parameters.AddWithValue("@startTime", lesson.StartTime);
                        cmd.Parameters.AddWithValue("@endTime", lesson.EndTime);
                        cmd.Parameters.AddWithValue("@room", lesson.Room ?? "");
                        cmd.Parameters.AddWithValue("@lessonPlanId", lesson.LessonPlanId.HasValue ? (object)lesson.LessonPlanId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@lessonPlanTitle", lesson.LessonPlanTitle ?? "");
                        cmd.Parameters.AddWithValue("@homework", lesson.Homework ?? "");
                        cmd.Parameters.AddWithValue("@note", lesson.Note ?? "");
                        cmd.Parameters.AddWithValue("@isCanceled", lesson.IsCanceled);

                        lesson.Id = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка добавления урока: {ex.Message}");
                MessageBox.Show($"Ошибка добавления урока: {ex.Message}");
            }
        }

        public void UpdateScheduleLesson(ScheduleLesson lesson)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    int subjectId = GetSubjectId(conn, lesson.Subject);
                    int classId = GetClassId(conn, lesson.Class);
                    int teacherId = GetTeacherIdByName(conn, lesson.Teacher);

                    string query = @"
                        UPDATE schedule_lessons 
                        SET subject_id = @subjectId,
                            subject = @subject,
                            class_id = @classId,
                            class_name = @className,
                            teacher_id = @teacherId,
                            teacher_name = @teacherName,
                            room = @room,
                            lesson_plan_id = @lessonPlanId,
                            lesson_plan_title = @lessonPlanTitle,
                            homework = @homework,
                            note = @note,
                            is_canceled = @isCanceled,
                            updated_at = NOW()
                        WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", lesson.Id);
                        cmd.Parameters.AddWithValue("@subjectId", subjectId);
                        cmd.Parameters.AddWithValue("@subject", lesson.Subject);
                        cmd.Parameters.AddWithValue("@classId", classId);
                        cmd.Parameters.AddWithValue("@className", lesson.Class);
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        cmd.Parameters.AddWithValue("@teacherName", lesson.Teacher);
                        cmd.Parameters.AddWithValue("@room", lesson.Room ?? "");
                        cmd.Parameters.AddWithValue("@lessonPlanId", lesson.LessonPlanId.HasValue ? (object)lesson.LessonPlanId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@lessonPlanTitle", lesson.LessonPlanTitle ?? "");
                        cmd.Parameters.AddWithValue("@homework", lesson.Homework ?? "");
                        cmd.Parameters.AddWithValue("@note", lesson.Note ?? "");
                        cmd.Parameters.AddWithValue("@isCanceled", lesson.IsCanceled);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления урока: {ex.Message}");
                MessageBox.Show($"Ошибка обновления урока: {ex.Message}");
            }
        }

        // ============================================================
        // ЗАМЕТКИ УЧИТЕЛЯ
        // ============================================================

        public void SaveTeacherNote(int userId, int scheduleLessonId, string noteText)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string checkQuery = "SELECT id FROM teacher_notes WHERE user_id = @userId AND schedule_lesson_id = @scheduleLessonId";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@userId", userId);
                        checkCmd.Parameters.AddWithValue("@scheduleLessonId", scheduleLessonId);
                        var existingId = checkCmd.ExecuteScalar();

                        if (existingId != null)
                        {
                            string updateQuery = @"
                                UPDATE teacher_notes 
                                SET note_text = @noteText, updated_at = NOW() 
                                WHERE id = @id";

                            using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", existingId);
                                cmd.Parameters.AddWithValue("@noteText", noteText);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            string insertQuery = @"
                                INSERT INTO teacher_notes (user_id, schedule_lesson_id, note_text)
                                VALUES (@userId, @scheduleLessonId, @noteText)";

                            using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@userId", userId);
                                cmd.Parameters.AddWithValue("@scheduleLessonId", scheduleLessonId);
                                cmd.Parameters.AddWithValue("@noteText", noteText);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения заметки: {ex.Message}");
                MessageBox.Show($"Ошибка сохранения заметки: {ex.Message}");
            }
        }

        // ============================================================
        // ШАБЛОНЫ ФГОС
        // ============================================================

        public List<FgosTemplate> GetFgosTemplates(string subject = null, string gradeLevel = null)
        {
            List<FgosTemplate> templates = new List<FgosTemplate>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT id, subject_id, subject, grade_level as grade, topic, goal, source
                        FROM fgos_templates 
                        WHERE is_active = TRUE";

                    if (!string.IsNullOrEmpty(subject))
                        query += " AND subject = @subject";
                    if (!string.IsNullOrEmpty(gradeLevel))
                        query += " AND grade_level = @gradeLevel";

                    query += " ORDER BY subject, grade_level, topic";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        if (!string.IsNullOrEmpty(subject))
                            cmd.Parameters.AddWithValue("@subject", subject);
                        if (!string.IsNullOrEmpty(gradeLevel))
                            cmd.Parameters.AddWithValue("@gradeLevel", gradeLevel);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                templates.Add(new FgosTemplate
                                {
                                    Id = reader.GetInt32("id"),
                                    Subject = reader.GetString("subject"),
                                    SubjectId = reader.GetInt32("subject_id"),
                                    Grade = reader.GetString("grade"),
                                    Topic = reader.GetString("topic"),
                                    Goal = reader.GetString("goal"),
                                    Source = reader.IsDBNull(reader.GetOrdinal("source")) ? "" : reader.GetString("source"),
                                    Tasks = new List<string>(),
                                    Stages = new List<LessonStage>()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки шаблонов: {ex.Message}");
            }

            return templates;
        }

        // ============================================================
        // СТУДЕНТЫ (УЧАЩИЕСЯ)
        // ============================================================

        public List<Student> GetAllStudents()
        {
            List<Student> students = new List<Student>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT * FROM students 
                        WHERE is_active = TRUE 
                        ORDER BY class_name, full_name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                students.Add(MapToStudent(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки учащихся: {ex.Message}");
            }

            return students;
        }

        public List<Student> GetStudentsByClass(string className)
        {
            List<Student> students = new List<Student>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT * FROM students 
                        WHERE class_name = @className AND is_active = TRUE 
                        ORDER BY full_name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@className", className);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                students.Add(MapToStudent(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки учащихся по классу: {ex.Message}");
            }

            return students;
        }

        public int AddStudent(Student student)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        INSERT INTO students 
                        (full_name, class_id, class_name, birth_date, gender, parent_name, parent_phone, address)
                        VALUES 
                        (@fullName, @classId, @className, @birthDate, @gender, @parentName, @parentPhone, @address);
                        SELECT LAST_INSERT_ID();";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@fullName", student.FullName);
                        cmd.Parameters.AddWithValue("@classId", student.ClassId);
                        cmd.Parameters.AddWithValue("@className", student.ClassName);
                        cmd.Parameters.AddWithValue("@birthDate", student.BirthDate.HasValue ? (object)student.BirthDate.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@gender", student.Gender ?? "");
                        cmd.Parameters.AddWithValue("@parentName", student.ParentName ?? "");
                        cmd.Parameters.AddWithValue("@parentPhone", student.ParentPhone ?? "");
                        cmd.Parameters.AddWithValue("@address", student.Address ?? "");

                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка добавления учащегося: {ex.Message}");
                return -1;
            }
        }

        public bool UpdateStudent(Student student)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        UPDATE students 
                        SET full_name = @fullName,
                            class_id = @classId,
                            class_name = @className,
                            birth_date = @birthDate,
                            gender = @gender,
                            parent_name = @parentName,
                            parent_phone = @parentPhone,
                            address = @address
                        WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", student.Id);
                        cmd.Parameters.AddWithValue("@fullName", student.FullName);
                        cmd.Parameters.AddWithValue("@classId", student.ClassId);
                        cmd.Parameters.AddWithValue("@className", student.ClassName);
                        cmd.Parameters.AddWithValue("@birthDate", student.BirthDate.HasValue ? (object)student.BirthDate.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@gender", student.Gender ?? "");
                        cmd.Parameters.AddWithValue("@parentName", student.ParentName ?? "");
                        cmd.Parameters.AddWithValue("@parentPhone", student.ParentPhone ?? "");
                        cmd.Parameters.AddWithValue("@address", student.Address ?? "");

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления учащегося: {ex.Message}");
                return false;
            }
        }

        public bool DeleteStudent(int studentId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "UPDATE students SET is_active = FALSE WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", studentId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления учащегося: {ex.Message}");
                return false;
            }
        }

        public bool HardDeleteStudent(int studentId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "DELETE FROM students WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", studentId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка полного удаления учащегося: {ex.Message}");
                return false;
            }
        }

        public int ImportStudents(List<Student> students)
        {
            int imported = 0;

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    foreach (var student in students)
                    {
                        string query = @"
                            INSERT INTO students 
                            (full_name, class_id, class_name, birth_date, gender, parent_name, parent_phone, address)
                            VALUES 
                            (@fullName, @classId, @className, @birthDate, @gender, @parentName, @parentPhone, @address)";

                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@fullName", student.FullName);
                            cmd.Parameters.AddWithValue("@classId", student.ClassId);
                            cmd.Parameters.AddWithValue("@className", student.ClassName);
                            cmd.Parameters.AddWithValue("@birthDate", student.BirthDate.HasValue ? (object)student.BirthDate.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@gender", student.Gender ?? "");
                            cmd.Parameters.AddWithValue("@parentName", student.ParentName ?? "");
                            cmd.Parameters.AddWithValue("@parentPhone", student.ParentPhone ?? "");
                            cmd.Parameters.AddWithValue("@address", student.Address ?? "");

                            if (cmd.ExecuteNonQuery() > 0)
                                imported++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка импорта учащихся: {ex.Message}");
            }

            return imported;
        }

        // ============================================================
        // ФАЙЛЫ ФГОС
        // ============================================================

        public class FgosFile
        {
            public int Id { get; set; }
            public int SubjectId { get; set; }
            public string SubjectName { get; set; }
            public string FileName { get; set; }
            public string FilePath { get; set; }
            public int FileSize { get; set; }
            public string FileSizeString => FormatFileSize(FileSize);
            public string FileType { get; set; }
            public string GradeLevel { get; set; }
            public string Variant { get; set; }
            public string Description { get; set; }
            public DateTime UploadDate { get; set; }
            public int UploadedBy { get; set; }
            public string UploadedByName { get; set; }
            public int DownloadCount { get; set; }
            public bool IsActive { get; set; }

            private string FormatFileSize(int bytes)
            {
                if (bytes < 1024) return $"{bytes} Б";
                if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} КБ";
                return $"{bytes / (1024 * 1024):F1} МБ";
            }
        }

        public List<FgosFile> GetAllFgosFiles(int? subjectId = null, string gradeLevel = null)
        {
            List<FgosFile> files = new List<FgosFile>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT f.*, u.full_name as uploaded_by_name
                        FROM fgos_files f
                        JOIN users u ON f.uploaded_by = u.id
                        WHERE f.is_active = TRUE";

                    if (subjectId.HasValue)
                        query += " AND f.subject_id = @subjectId";
                    if (!string.IsNullOrEmpty(gradeLevel))
                        query += " AND f.grade_level LIKE @gradeLevel";

                    query += " ORDER BY f.subject_name, f.grade_level, f.upload_date DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        if (subjectId.HasValue)
                            cmd.Parameters.AddWithValue("@subjectId", subjectId.Value);
                        if (!string.IsNullOrEmpty(gradeLevel))
                            cmd.Parameters.AddWithValue("@gradeLevel", $"%{gradeLevel}%");

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                files.Add(new FgosFile
                                {
                                    Id = reader.GetInt32("id"),
                                    SubjectId = reader.GetInt32("subject_id"),
                                    SubjectName = reader.GetString("subject_name"),
                                    FileName = reader.GetString("file_name"),
                                    FilePath = reader.GetString("file_path"),
                                    FileSize = reader.GetInt32("file_size"),
                                    FileType = reader.GetString("file_type"),
                                    GradeLevel = reader.IsDBNull(reader.GetOrdinal("grade_level")) ? "" : reader.GetString("grade_level"),
                                    Variant = reader.IsDBNull(reader.GetOrdinal("variant")) ? "" : reader.GetString("variant"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                                    UploadDate = reader.GetDateTime("upload_date"),
                                    UploadedBy = reader.GetInt32("uploaded_by"),
                                    UploadedByName = reader.GetString("uploaded_by_name"),
                                    DownloadCount = reader.GetInt32("download_count"),
                                    IsActive = reader.GetBoolean("is_active")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки файлов ФГОС: {ex.Message}");
            }

            return files;
        }

        public int AddFgosFile(FgosFile file)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        INSERT INTO fgos_files 
                        (subject_id, subject_name, file_name, file_path, file_size, file_type, 
                         grade_level, variant, description, uploaded_by)
                        VALUES 
                        (@subjectId, @subjectName, @fileName, @filePath, @fileSize, @fileType,
                         @gradeLevel, @variant, @description, @uploadedBy);
                        SELECT LAST_INSERT_ID();";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@subjectId", file.SubjectId);
                        cmd.Parameters.AddWithValue("@subjectName", file.SubjectName);
                        cmd.Parameters.AddWithValue("@fileName", file.FileName);
                        cmd.Parameters.AddWithValue("@filePath", file.FilePath);
                        cmd.Parameters.AddWithValue("@fileSize", file.FileSize);
                        cmd.Parameters.AddWithValue("@fileType", file.FileType ?? "");
                        cmd.Parameters.AddWithValue("@gradeLevel", file.GradeLevel ?? "");
                        cmd.Parameters.AddWithValue("@variant", file.Variant ?? "");
                        cmd.Parameters.AddWithValue("@description", file.Description ?? "");
                        cmd.Parameters.AddWithValue("@uploadedBy", file.UploadedBy);

                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка добавления файла ФГОС: {ex.Message}");
                return -1;
            }
        }

        public void IncrementFgosDownloadCount(int fileId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "UPDATE fgos_files SET download_count = download_count + 1 WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", fileId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления счетчика скачиваний: {ex.Message}");
            }
        }

        public bool DeleteFgosFile(int fileId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "UPDATE fgos_files SET is_active = FALSE WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", fileId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления файла ФГОС: {ex.Message}");
                return false;
            }
        }

        public bool AttachFgosToLessonPlan(int lessonPlanId, int fgosFileId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        INSERT INTO lesson_plan_fgos (lesson_plan_id, fgos_file_id)
                        VALUES (@lessonPlanId, @fgosFileId)
                        ON DUPLICATE KEY UPDATE id = id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@lessonPlanId", lessonPlanId);
                        cmd.Parameters.AddWithValue("@fgosFileId", fgosFileId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка привязки файла к плану: {ex.Message}");
                return false;
            }
        }

        public bool DetachFgosFromLessonPlan(int lessonPlanId, int fgosFileId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "DELETE FROM lesson_plan_fgos WHERE lesson_plan_id = @lessonPlanId AND fgos_file_id = @fgosFileId";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@lessonPlanId", lessonPlanId);
                        cmd.Parameters.AddWithValue("@fgosFileId", fgosFileId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отвязки файла от плана: {ex.Message}");
                return false;
            }
        }

        public List<FgosFile> GetFgosFilesForLessonPlan(int lessonPlanId)
        {
            List<FgosFile> files = new List<FgosFile>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT f.*, u.full_name as uploaded_by_name
                        FROM fgos_files f
                        JOIN lesson_plan_fgos lpf ON f.id = lpf.fgos_file_id
                        JOIN users u ON f.uploaded_by = u.id
                        WHERE lpf.lesson_plan_id = @lessonPlanId AND f.is_active = TRUE
                        ORDER BY f.upload_date DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@lessonPlanId", lessonPlanId);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                files.Add(new FgosFile
                                {
                                    Id = reader.GetInt32("id"),
                                    SubjectId = reader.GetInt32("subject_id"),
                                    SubjectName = reader.GetString("subject_name"),
                                    FileName = reader.GetString("file_name"),
                                    FilePath = reader.GetString("file_path"),
                                    FileSize = reader.GetInt32("file_size"),
                                    FileType = reader.GetString("file_type"),
                                    GradeLevel = reader.IsDBNull(reader.GetOrdinal("grade_level")) ? "" : reader.GetString("grade_level"),
                                    Variant = reader.IsDBNull(reader.GetOrdinal("variant")) ? "" : reader.GetString("variant"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                                    UploadDate = reader.GetDateTime("upload_date"),
                                    UploadedBy = reader.GetInt32("uploaded_by"),
                                    UploadedByName = reader.GetString("uploaded_by_name"),
                                    DownloadCount = reader.GetInt32("download_count"),
                                    IsActive = reader.GetBoolean("is_active")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки файлов для плана: {ex.Message}");
            }

            return files;
        }

        // ============================================================
        // УДАЛЕНИЕ РАСПИСАНИЯ
        // ============================================================

        public bool HardDeleteSchedule(int scheduleId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string deleteLessonsQuery = "DELETE FROM schedule_lessons WHERE schedule_id = @scheduleId";
                            using (MySqlCommand cmd = new MySqlCommand(deleteLessonsQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@scheduleId", scheduleId);
                                cmd.ExecuteNonQuery();
                            }

                            string deleteScheduleQuery = "DELETE FROM schedules WHERE id = @id";
                            using (MySqlCommand cmd = new MySqlCommand(deleteScheduleQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", scheduleId);
                                int result = cmd.ExecuteNonQuery();

                                transaction.Commit();
                                return result > 0;
                            }
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка полного удаления расписания: {ex.Message}");
                return false;
            }
        }

        public bool SoftDeleteSchedule(int scheduleId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "UPDATE schedules SET is_active = FALSE WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", scheduleId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка мягкого удаления расписания: {ex.Message}");
                return false;
            }
        }

        // ============================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ============================================================

        private int GetSubjectId(MySqlConnection conn, string subjectName)
        {
            if (string.IsNullOrEmpty(subjectName)) return 0;

            try
            {
                // Проверяем существование
                string query = "SELECT id FROM subjects WHERE name = @name";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@name", subjectName);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        return Convert.ToInt32(result);
                }

                // Если не существует - создаем (НО С ПРОВЕРКОЙ на дубли)
                // Используем INSERT IGNORE чтобы избежать дублей при параллельных запросах
                string insertQuery = @"
            INSERT IGNORE INTO subjects (name, is_active, hours_per_week) 
            VALUES (@name, 1, 2);
            SELECT id FROM subjects WHERE name = @name;";

                using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@name", subjectName);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        return Convert.ToInt32(result);
                }

                // Если все еще не получилось - последняя попытка
                string lastQuery = "SELECT id FROM subjects WHERE name = @name";
                using (MySqlCommand cmd = new MySqlCommand(lastQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@name", subjectName);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        return Convert.ToInt32(result);
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetSubjectId: {ex.Message}");
                return 0;
            }
        }
        /// <summary>
        /// Нормализует все предметы: удаляет дубли, создает связи в plan_subjects
        /// </summary>
        public bool NormalizeSubjects()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Удаляем дубликаты из subjects
                            string deleteDupes = @"
                        DELETE s1 FROM subjects s1
                        INNER JOIN subjects s2 
                        WHERE s1.id > s2.id AND s1.name = s2.name";

                            using (MySqlCommand cmd = new MySqlCommand(deleteDupes, conn, transaction))
                            {
                                cmd.ExecuteNonQuery();
                            }

                            // 2. Добавляем недостающие предметы из plan_subjects
                            string addMissing = @"
                        INSERT IGNORE INTO subjects (name, is_active, hours_per_week)
                        SELECT DISTINCT ps.subject_name, 1, 2
                        FROM plan_subjects ps
                        WHERE NOT EXISTS (
                            SELECT 1 FROM subjects s WHERE s.name = ps.subject_name
                        )";

                            using (MySqlCommand cmd = new MySqlCommand(addMissing, conn, transaction))
                            {
                                cmd.ExecuteNonQuery();
                            }

                            // 3. Обновляем subject_id в plan_subjects
                            string updatePlanSubjects = @"
                        UPDATE plan_subjects ps
                        JOIN subjects s ON s.name = ps.subject_name
                        SET ps.subject_id = s.id
                        WHERE ps.subject_id IS NULL OR ps.subject_id != s.id";

                            using (MySqlCommand cmd = new MySqlCommand(updatePlanSubjects, conn, transaction))
                            {
                                cmd.ExecuteNonQuery();
                            }

                            // 4. Удаляем дубли в plan_subjects
                            string deleteDupesPlan = @"
                        DELETE ps1 FROM plan_subjects ps1
                        INNER JOIN plan_subjects ps2
                        WHERE ps1.id > ps2.id 
                          AND ps1.plan_id = ps2.plan_id
                          AND ps1.subject_name = ps2.subject_name
                          AND ps1.grade = ps2.grade";

                            using (MySqlCommand cmd = new MySqlCommand(deleteDupesPlan, conn, transaction))
                            {
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine("Нормализация предметов выполнена успешно");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"Ошибка нормализации: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка NormalizeSubjects: {ex.Message}");
                return false;
            }
        }
        private int GetClassId(MySqlConnection conn, string className)
        {
            if (string.IsNullOrEmpty(className)) return 0;

            try
            {
                string query = "SELECT id FROM classes WHERE name = @name";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@name", className);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        return Convert.ToInt32(result);
                }

                int grade = GetGradeFromClassName(className);
                string insertQuery = "INSERT INTO classes (name, grade_level) VALUES (@name, @grade); SELECT LAST_INSERT_ID();";
                using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@name", className);
                    cmd.Parameters.AddWithValue("@grade", grade);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetClassId: {ex.Message}");
                return 0;
            }
        }

        private int GetTeacherIdByName(MySqlConnection conn, string teacherName)
        {
            if (string.IsNullOrEmpty(teacherName))
                return 1; // Администратор

            try
            {
                // ✅ ИЩЕМ ТОЛЬКО В USERS
                string query = "SELECT id FROM users WHERE full_name = @name AND role = 'Teacher'";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@name", teacherName);
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                        return Convert.ToInt32(result);
                }

                // Если учитель не найден, создаем его в users
                string username = teacherName
                    .ToLower()
                    .Replace(" ", "_")
                    .Replace(".", "_")
                    .Replace(",", "_")
                    .Replace("-", "_")
                    .Replace("ё", "е")
                    .Replace("й", "и");

                if (username.Length > 20)
                    username = username.Substring(0, 20);

                string finalUsername = username;
                int counter = 1;
                while (true)
                {
                    string checkQuery = "SELECT COUNT(*) FROM users WHERE username = @username";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@username", finalUsername);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());
                        if (count == 0)
                            break;
                        finalUsername = username + counter;
                        counter++;
                    }
                }

                string insertQuery = @"
            INSERT INTO users (full_name, username, password_hash, role, subject, max_hours_per_week) 
            VALUES (@name, @username, '123', 'Teacher', '', 20)";
                using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@name", teacherName);
                    insertCmd.Parameters.AddWithValue("@username", finalUsername);
                    insertCmd.ExecuteNonQuery();
                    return (int)insertCmd.LastInsertedId;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetTeacherIdByName: {ex.Message}");
                return 1;
            }
        }

        private LessonPlan MapToLessonPlan(MySqlDataReader reader)
        {
            return new LessonPlan
            {
                Id = reader.GetInt32("id"),
                Title = reader.GetString("title"),
                Subject = reader.GetString("subject_name"),
                SubjectId = reader.GetInt32("subject_id"),
                Class = reader.GetString("class_name"),
                ClassId = reader.GetInt32("class_id"),
                Goal = reader.GetString("goal"),
                TeacherId = reader.GetInt32("teacher_id"),
                TeacherName = reader.GetString("teacher_name"),
                CreatedDate = reader.GetDateTime("created_date"),
                Status = (LessonStatus)Enum.Parse(typeof(LessonStatus), reader.GetString("status")),
                IsDeleted = reader.GetBoolean("is_deleted"),
                ReviewedBy = reader.IsDBNull(reader.GetOrdinal("reviewed_by")) ? (int?)null : reader.GetInt32("reviewed_by"),
                ReviewedDate = reader.IsDBNull(reader.GetOrdinal("reviewed_date")) ? (DateTime?)null : reader.GetDateTime("reviewed_date")
            };
        }

        private void LoadLessonDetails(LessonPlan plan)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string tasksQuery = @"
                        SELECT task_text FROM lesson_tasks 
                        WHERE lesson_plan_id = @planId 
                        ORDER BY sort_order";

                    using (MySqlCommand cmd = new MySqlCommand(tasksQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@planId", plan.Id);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                plan.Tasks.Add(reader.GetString("task_text"));
                            }
                        }
                    }

                    string stagesQuery = @"
                        SELECT name, duration, description, example 
                        FROM lesson_stages 
                        WHERE lesson_plan_id = @planId 
                        ORDER BY sort_order";

                    using (MySqlCommand cmd = new MySqlCommand(stagesQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@planId", plan.Id);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                plan.Stages.Add(new LessonStage
                                {
                                    Name = reader.GetString("name"),
                                    Duration = reader.GetInt32("duration"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                                    Example = reader.IsDBNull(reader.GetOrdinal("example")) ? "" : reader.GetString("example")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки деталей плана: {ex.Message}");
            }
        }

        private void LoadComments(LessonPlan plan)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT * FROM comments 
                        WHERE lesson_plan_id = @planId 
                        ORDER BY date DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@planId", plan.Id);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                plan.Comments.Add(new Comment
                                {
                                    Id = reader.GetInt32("id"),
                                    UserId = reader.GetInt32("user_id"),
                                    UserName = reader.GetString("user_name"),
                                    Text = reader.GetString("text"),
                                    Date = reader.GetDateTime("date")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки комментариев: {ex.Message}");
            }
        }

        private void LoadScheduleLessons(Schedule schedule)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT * FROM schedule_lessons 
                        WHERE schedule_id = @scheduleId 
                        ORDER BY day_of_week, lesson_number";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@scheduleId", schedule.Id);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                schedule.Lessons.Add(new ScheduleLesson
                                {
                                    Id = reader.GetInt32("id"),
                                    ScheduleId = reader.GetInt32("schedule_id"),
                                    Subject = reader.GetString("subject"),
                                    SubjectId = reader.GetInt32("subject_id"),
                                    Class = reader.GetString("class_name"),
                                    ClassId = reader.GetInt32("class_id"),
                                    Teacher = reader.GetString("teacher_name"),
                                    TeacherId = reader.GetInt32("teacher_id"),
                                    DayOfWeek = reader.GetString("day_of_week"),
                                    LessonNumber = reader.GetInt32("lesson_number"),
                                    StartTime = reader.GetTimeSpan("start_time"),
                                    EndTime = reader.GetTimeSpan("end_time"),
                                    Room = reader.IsDBNull(reader.GetOrdinal("room")) ? "" : reader.GetString("room"),
                                    LessonPlanId = reader.IsDBNull(reader.GetOrdinal("lesson_plan_id")) ? (int?)null : reader.GetInt32("lesson_plan_id"),
                                    LessonPlanTitle = reader.IsDBNull(reader.GetOrdinal("lesson_plan_title")) ? "" : reader.GetString("lesson_plan_title"),
                                    Homework = reader.IsDBNull(reader.GetOrdinal("homework")) ? "" : reader.GetString("homework"),
                                    Note = reader.IsDBNull(reader.GetOrdinal("note")) ? "" : reader.GetString("note"),
                                    IsCanceled = reader.GetBoolean("is_canceled")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки уроков: {ex.Message}");
            }
        }

        private Student MapToStudent(MySqlDataReader reader)
        {
            return new Student
            {
                Id = reader.GetInt32("id"),
                FullName = reader.GetString("full_name"),
                ClassId = reader.GetInt32("class_id"),
                ClassName = reader.GetString("class_name"),
                BirthDate = reader.IsDBNull(reader.GetOrdinal("birth_date")) ? (DateTime?)null : reader.GetDateTime("birth_date"),
                Gender = reader.IsDBNull(reader.GetOrdinal("gender")) ? null : reader.GetString("gender"),
                ParentName = reader.IsDBNull(reader.GetOrdinal("parent_name")) ? null : reader.GetString("parent_name"),
                ParentPhone = reader.IsDBNull(reader.GetOrdinal("parent_phone")) ? null : reader.GetString("parent_phone"),
                Address = reader.IsDBNull(reader.GetOrdinal("address")) ? null : reader.GetString("address"),
                IsActive = reader.GetBoolean("is_active"),
                CreatedAt = reader.GetDateTime("created_at"),
                UpdatedAt = reader.GetDateTime("updated_at")
            };
        }

        // ============================================================
        // СТАТИСТИКА ПО КЛАССАМ
        // ============================================================

        public class ClassStats
        {
            public string ClassName { get; set; }
            public int StudentCount { get; set; }
            public int BoysCount { get; set; }
            public int GirlsCount { get; set; }
        }

        public List<ClassStats> GetClassStats()
        {
            List<ClassStats> stats = new List<ClassStats>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    class_name,
                    COUNT(*) as total,
                    SUM(CASE WHEN gender = 'М' THEN 1 ELSE 0 END) as boys,
                    SUM(CASE WHEN gender = 'Ж' THEN 1 ELSE 0 END) as girls
                FROM students 
                WHERE is_active = TRUE 
                GROUP BY class_name 
                ORDER BY class_name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                stats.Add(new ClassStats
                                {
                                    ClassName = reader.GetString("class_name"),
                                    StudentCount = reader.GetInt32("total"),
                                    BoysCount = reader.GetInt32("boys"),
                                    GirlsCount = reader.GetInt32("girls")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки статистики по классам: {ex.Message}");
            }

            return stats;
        }
        // ============================================================
        // СТУДЕНТЫ (УЧАЩИЕСЯ)
        // ============================================================

        public class Student
        {
            private readonly string connectionString;

            public int Id { get; set; }
            public string FullName { get; set; }
            public int ClassId { get; set; }
            public string ClassName { get; set; }
            public DateTime? BirthDate { get; set; }
            public string Gender { get; set; }
            public string ParentName { get; set; }
            public string ParentPhone { get; set; }
            public string Address { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }

            public List<Student> GetAllStudents()
            {
                List<Student> students = new List<Student>();

                try
                {
                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();

                        string query = @"
                SELECT * FROM students 
                WHERE is_active = TRUE 
                ORDER BY class_name, full_name";

                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    students.Add(MapToStudent(reader));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки учащихся: {ex.Message}");
                }

                return students;
            }

            public List<Student> GetStudentsByClass(string className)
            {
                List<Student> students = new List<Student>();

                try
                {
                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();

                        string query = @"
                SELECT * FROM students 
                WHERE class_name = @className AND is_active = TRUE 
                ORDER BY full_name";

                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@className", className);

                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    students.Add(MapToStudent(reader));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки учащихся по классу: {ex.Message}");
                }

                return students;
            }

            public int AddStudent(Student student)
            {
                try
                {
                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();

                        string query = @"
                INSERT INTO students 
                (full_name, class_id, class_name, birth_date, gender, parent_name, parent_phone, address)
                VALUES 
                (@fullName, @classId, @className, @birthDate, @gender, @parentName, @parentPhone, @address);
                SELECT LAST_INSERT_ID();";

                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@fullName", student.FullName);
                            cmd.Parameters.AddWithValue("@classId", student.ClassId);
                            cmd.Parameters.AddWithValue("@className", student.ClassName);
                            cmd.Parameters.AddWithValue("@birthDate", student.BirthDate.HasValue ? (object)student.BirthDate.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@gender", student.Gender ?? "");
                            cmd.Parameters.AddWithValue("@parentName", student.ParentName ?? "");
                            cmd.Parameters.AddWithValue("@parentPhone", student.ParentPhone ?? "");
                            cmd.Parameters.AddWithValue("@address", student.Address ?? "");

                            return Convert.ToInt32(cmd.ExecuteScalar());
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка добавления учащегося: {ex.Message}");
                    return -1;
                }
            }

            public bool UpdateStudent(Student student)
            {
                try
                {
                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();

                        string query = @"
                UPDATE students 
                SET full_name = @fullName,
                    class_id = @classId,
                    class_name = @className,
                    birth_date = @birthDate,
                    gender = @gender,
                    parent_name = @parentName,
                    parent_phone = @parentPhone,
                    address = @address
                WHERE id = @id";

                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", student.Id);
                            cmd.Parameters.AddWithValue("@fullName", student.FullName);
                            cmd.Parameters.AddWithValue("@classId", student.ClassId);
                            cmd.Parameters.AddWithValue("@className", student.ClassName);
                            cmd.Parameters.AddWithValue("@birthDate", student.BirthDate.HasValue ? (object)student.BirthDate.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@gender", student.Gender ?? "");
                            cmd.Parameters.AddWithValue("@parentName", student.ParentName ?? "");
                            cmd.Parameters.AddWithValue("@parentPhone", student.ParentPhone ?? "");
                            cmd.Parameters.AddWithValue("@address", student.Address ?? "");

                            return cmd.ExecuteNonQuery() > 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка обновления учащегося: {ex.Message}");
                    return false;
                }
            }

            public bool DeleteStudent(int studentId)
            {
                try
                {
                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();

                        string query = "UPDATE students SET is_active = FALSE WHERE id = @id";

                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", studentId);

                            return cmd.ExecuteNonQuery() > 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка удаления учащегося: {ex.Message}");
                    return false;
                }
            }

            public bool HardDeleteStudent(int studentId)
            {
                try
                {
                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();

                        string query = "DELETE FROM students WHERE id = @id";

                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", studentId);

                            return cmd.ExecuteNonQuery() > 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка полного удаления учащегося: {ex.Message}");
                    return false;
                }
            }

            public int ImportStudents(List<Student> students)
            {
                int imported = 0;

                try
                {
                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();

                        foreach (var student in students)
                        {
                            string query = @"
                    INSERT INTO students 
                    (full_name, class_id, class_name, birth_date, gender, parent_name, parent_phone, address)
                    VALUES 
                    (@fullName, @classId, @className, @birthDate, @gender, @parentName, @parentPhone, @address)";

                            using (MySqlCommand cmd = new MySqlCommand(query, conn))
                            {
                                cmd.Parameters.AddWithValue("@fullName", student.FullName);
                                cmd.Parameters.AddWithValue("@classId", student.ClassId);
                                cmd.Parameters.AddWithValue("@className", student.ClassName);
                                cmd.Parameters.AddWithValue("@birthDate", student.BirthDate.HasValue ? (object)student.BirthDate.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@gender", student.Gender ?? "");
                                cmd.Parameters.AddWithValue("@parentName", student.ParentName ?? "");
                                cmd.Parameters.AddWithValue("@parentPhone", student.ParentPhone ?? "");
                                cmd.Parameters.AddWithValue("@address", student.Address ?? "");

                                if (cmd.ExecuteNonQuery() > 0)
                                    imported++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка импорта учащихся: {ex.Message}");
                }

                return imported;
            }

            private Student MapToStudent(MySqlDataReader reader)
            {
                return new Student
                {
                    Id = reader.GetInt32("id"),
                    FullName = reader.GetString("full_name"),
                    ClassId = reader.GetInt32("class_id"),
                    ClassName = reader.GetString("class_name"),
                    BirthDate = reader.IsDBNull(reader.GetOrdinal("birth_date")) ? (DateTime?)null : reader.GetDateTime("birth_date"),
                    Gender = reader.IsDBNull(reader.GetOrdinal("gender")) ? null : reader.GetString("gender"),
                    ParentName = reader.IsDBNull(reader.GetOrdinal("parent_name")) ? null : reader.GetString("parent_name"),
                    ParentPhone = reader.IsDBNull(reader.GetOrdinal("parent_phone")) ? null : reader.GetString("parent_phone"),
                    Address = reader.IsDBNull(reader.GetOrdinal("address")) ? null : reader.GetString("address"),
                    IsActive = reader.GetBoolean("is_active"),
                    CreatedAt = reader.GetDateTime("created_at"),
                    UpdatedAt = reader.GetDateTime("updated_at")
                };
            }

        }
        // ============================================================
        // КЛАССЫ С ИНФОРМАЦИЕЙ
        // ============================================================

        public class ClassInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Grade { get; set; }
            public int StudentCount { get; set; }
            public string PlanName { get; set; }
            public int? PlanId { get; set; }
        }


        // В DbHelper.cs добавьте метод удаления плана урока
        /// <summary>
        /// Удаляет план урока (с отвязкой от расписания)
        /// </summary>
        public bool DeleteLessonPlan(int planId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Отвязываем от расписания
                            string detachQuery = @"
                        UPDATE schedule_lessons 
                        SET lesson_plan_id = NULL,
                            lesson_plan_title = NULL
                        WHERE lesson_plan_id = @planId";

                            using (MySqlCommand cmd = new MySqlCommand(detachQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@planId", planId);
                                int detached = cmd.ExecuteNonQuery();
                                System.Diagnostics.Debug.WriteLine($"Отвязано уроков: {detached}");
                            }

                            // 2. Удаляем задачи
                            string deleteTasksQuery = "DELETE FROM lesson_tasks WHERE lesson_plan_id = @planId";
                            using (MySqlCommand cmd = new MySqlCommand(deleteTasksQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@planId", planId);
                                cmd.ExecuteNonQuery();
                            }

                            // 3. Удаляем этапы
                            string deleteStagesQuery = "DELETE FROM lesson_stages WHERE lesson_plan_id = @planId";
                            using (MySqlCommand cmd = new MySqlCommand(deleteStagesQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@planId", planId);
                                cmd.ExecuteNonQuery();
                            }

                            // 4. Удаляем комментарии
                            string deleteCommentsQuery = "DELETE FROM comments WHERE lesson_plan_id = @planId";
                            using (MySqlCommand cmd = new MySqlCommand(deleteCommentsQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@planId", planId);
                                cmd.ExecuteNonQuery();
                            }

                            // 5. Удаляем связи с файлами ФГОС (если таблица существует)
                            try
                            {
                                string checkTableQuery = @"
                            SELECT COUNT(*) 
                            FROM information_schema.tables 
                            WHERE table_schema = 'schoolplanner' 
                            AND table_name = 'lesson_plan_fgos'";

                                using (MySqlCommand checkCmd = new MySqlCommand(checkTableQuery, conn, transaction))
                                {
                                    int tableExists = Convert.ToInt32(checkCmd.ExecuteScalar());
                                    if (tableExists > 0)
                                    {
                                        string deleteFgosQuery = "DELETE FROM lesson_plan_fgos WHERE lesson_plan_id = @planId";
                                        using (MySqlCommand cmd = new MySqlCommand(deleteFgosQuery, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@planId", planId);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Таблица lesson_plan_fgos не существует: {ex.Message}");
                                // Продолжаем выполнение
                            }

                            // 6. Удаляем связи с файлами (если есть другая таблица для файлов)
                            try
                            {
                                string checkAttachmentsQuery = @"
                            SELECT COUNT(*) 
                            FROM information_schema.tables 
                            WHERE table_schema = 'schoolplanner' 
                            AND table_name = 'lesson_plan_attachments'";

                                using (MySqlCommand checkCmd = new MySqlCommand(checkAttachmentsQuery, conn, transaction))
                                {
                                    int tableExists = Convert.ToInt32(checkCmd.ExecuteScalar());
                                    if (tableExists > 0)
                                    {
                                        string deleteAttachmentsQuery = "DELETE FROM lesson_plan_attachments WHERE lesson_plan_id = @planId";
                                        using (MySqlCommand cmd = new MySqlCommand(deleteAttachmentsQuery, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@planId", planId);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Таблица lesson_plan_attachments не существует: {ex.Message}");
                                // Продолжаем выполнение
                            }

                            // 7. Удаляем сам план
                            string deletePlanQuery = "DELETE FROM lesson_plans WHERE id = @id";
                            using (MySqlCommand cmd = new MySqlCommand(deletePlanQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", planId);
                                int result = cmd.ExecuteNonQuery();

                                if (result > 0)
                                {
                                    transaction.Commit();
                                    System.Diagnostics.Debug.WriteLine($"План {planId} успешно удален");
                                    return true;
                                }
                                else
                                {
                                    transaction.Rollback();
                                    System.Diagnostics.Debug.WriteLine($"План {planId} не найден");
                                    return false;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"Ошибка в транзакции: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления плана урока: {ex.Message}");
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        // ============================================================
        // ОЦЕНКИ
        // ============================================================

        public List<Grade> GetGradesForStudent(int studentId)
        {
            var result = new List<Grade>();
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT * FROM grades WHERE student_id = @studentId ORDER BY date DESC";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@studentId", studentId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(new Grade
                                {
                                    Id = reader.GetInt32("id"),
                                    StudentId = reader.GetInt32("student_id"),
                                    Subject = reader.GetString("subject"),
                                    Value = reader.GetInt32("grade"),
                                    Date = reader.GetDateTime("date"),
                                    Comment = reader.IsDBNull(reader.GetOrdinal("comment")) ? "" : reader.GetString("comment"),
                                    LessonTitle = reader.IsDBNull(reader.GetOrdinal("lesson_title")) ? "" : reader.GetString("lesson_title")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetGradesForStudent: {ex.Message}");
            }
            return result;
        }

        public bool SaveGrade(Grade grade)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query;
                    if (grade.Id == 0)
                    {
                        query = @"INSERT INTO grades (student_id, subject, grade, date, comment, lesson_title) 
                         VALUES (@studentId, @subject, @grade, @date, @comment, @lessonTitle)";
                    }
                    else
                    {
                        query = @"UPDATE grades SET subject = @subject, grade = @grade, date = @date, 
                         comment = @comment, lesson_title = @lessonTitle WHERE id = @id";
                    }
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        if (grade.Id > 0) cmd.Parameters.AddWithValue("@id", grade.Id);
                        cmd.Parameters.AddWithValue("@studentId", grade.StudentId);
                        cmd.Parameters.AddWithValue("@subject", grade.Subject);
                        cmd.Parameters.AddWithValue("@grade", grade.Value);
                        cmd.Parameters.AddWithValue("@date", grade.Date);
                        cmd.Parameters.AddWithValue("@comment", grade.Comment ?? "");
                        cmd.Parameters.AddWithValue("@lessonTitle", grade.LessonTitle ?? "");
                        int result = cmd.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SaveGrade: {ex.Message}");
                return false;
            }
        }

        public bool DeleteGrade(int gradeId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "DELETE FROM grades WHERE id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", gradeId);
                        int result = cmd.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка DeleteGrade: {ex.Message}");
                return false;
            }
        }

        // ============================================================
        // ПОСЕЩАЕМОСТЬ
        // ============================================================

        public List<Attendance> GetAttendanceForStudent(int studentId)
        {
            var result = new List<Attendance>();
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT * FROM attendance WHERE student_id = @studentId ORDER BY date DESC";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@studentId", studentId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(new Attendance
                                {
                                    Id = reader.GetInt32("id"),
                                    StudentId = reader.GetInt32("student_id"),
                                    Date = reader.GetDateTime("date"),
                                    Status = (AttendanceStatus)Enum.Parse(typeof(AttendanceStatus), reader.GetString("status")),
                                    Note = reader.IsDBNull(reader.GetOrdinal("note")) ? "" : reader.GetString("note"),
                                    LessonTitle = reader.IsDBNull(reader.GetOrdinal("lesson_title")) ? "" : reader.GetString("lesson_title")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetAttendanceForStudent: {ex.Message}");
            }
            return result;
        }

        public bool SaveAttendance(Attendance attendance)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query;
                    if (attendance.Id == 0)
                    {
                        query = @"INSERT INTO attendance (student_id, date, status, note, lesson_title) 
                         VALUES (@studentId, @date, @status, @note, @lessonTitle)";
                    }
                    else
                    {
                        query = @"UPDATE attendance SET date = @date, status = @status, 
                         note = @note, lesson_title = @lessonTitle WHERE id = @id";
                    }
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        if (attendance.Id > 0) cmd.Parameters.AddWithValue("@id", attendance.Id);
                        cmd.Parameters.AddWithValue("@studentId", attendance.StudentId);
                        cmd.Parameters.AddWithValue("@date", attendance.Date);
                        cmd.Parameters.AddWithValue("@status", attendance.Status.ToString());
                        cmd.Parameters.AddWithValue("@note", attendance.Note ?? "");
                        cmd.Parameters.AddWithValue("@lessonTitle", attendance.LessonTitle ?? "");
                        int result = cmd.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SaveAttendance: {ex.Message}");
                return false;
            }
        }

        public bool DeleteAttendance(int attendanceId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "DELETE FROM attendance WHERE id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", attendanceId);
                        int result = cmd.ExecuteNonQuery();
                        return result > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка DeleteAttendance: {ex.Message}");
                return false;
            }
        }

        // ============================================================
        // ЛОГИРОВАНИЕ ИЗМЕНЕНИЙ
        // ============================================================

        public bool SaveLog(LogEntry log)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    // НЕ УКАЗЫВАЕМ id - он автоинкрементный
                    string query = @"INSERT INTO journal_log 
                (user_id, user_name, action, student_id, student_name, subject, old_value, new_value, date, comment)
                VALUES (@userId, @userName, @action, @studentId, @studentName, @subject, @oldValue, @newValue, @date, @comment)";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", log.UserId);
                        cmd.Parameters.AddWithValue("@userName", log.UserName);
                        cmd.Parameters.AddWithValue("@action", log.Action);
                        cmd.Parameters.AddWithValue("@studentId", log.StudentId);
                        cmd.Parameters.AddWithValue("@studentName", log.StudentName);
                        cmd.Parameters.AddWithValue("@subject", log.Subject ?? "");
                        cmd.Parameters.AddWithValue("@oldValue", log.OldValue ?? "");
                        cmd.Parameters.AddWithValue("@newValue", log.NewValue ?? "");
                        cmd.Parameters.AddWithValue("@date", log.Date);
                        cmd.Parameters.AddWithValue("@comment", log.Comment ?? "");

                        int result = cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"SaveLog результат: {result}");
                        return result > 0;
                    }
                }
            }
            catch (MySqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"MySQL ошибка SaveLog: {ex.Message}");
                if (ex.Message.Contains("doesn't have a default value"))
                {
                    MessageBox.Show("Ошибка структуры таблицы. Выполните SQL запрос для исправления таблицы journal_log.",
                        "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SaveLog: {ex.Message}");
                return false;
            }
        }
        public List<LogEntry> GetLogsForStudent(int studentId)
        {
            var logs = new List<LogEntry>();
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT * FROM journal_log 
                WHERE student_id = @studentId 
                ORDER BY date DESC LIMIT 100";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@studentId", studentId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                logs.Add(new LogEntry
                                {
                                    Id = reader.GetInt32("id"),
                                    UserId = reader.GetInt32("user_id"),
                                    UserName = reader.GetString("user_name"),
                                    Action = reader.GetString("action"),
                                    StudentId = reader.GetInt32("student_id"),
                                    StudentName = reader.GetString("student_name"),
                                    Subject = reader.IsDBNull(reader.GetOrdinal("subject")) ? "" : reader.GetString("subject"),
                                    OldValue = reader.IsDBNull(reader.GetOrdinal("old_value")) ? "" : reader.GetString("old_value"),
                                    NewValue = reader.IsDBNull(reader.GetOrdinal("new_value")) ? "" : reader.GetString("new_value"),
                                    Date = reader.GetDateTime("date"),
                                    Comment = reader.IsDBNull(reader.GetOrdinal("comment")) ? "" : reader.GetString("comment")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetLogsForStudent: {ex.Message}");
            }
            return logs;
        }

        public List<LogEntry> GetLogsForPeriod(DateTime startDate, DateTime endDate)
        {
            var logs = new List<LogEntry>();
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT * FROM journal_log 
                WHERE date BETWEEN @startDate AND @endDate 
                ORDER BY date DESC LIMIT 500";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@startDate", startDate);
                        cmd.Parameters.AddWithValue("@endDate", endDate);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                logs.Add(new LogEntry
                                {
                                    Id = reader.GetInt32("id"),
                                    UserId = reader.GetInt32("user_id"),
                                    UserName = reader.GetString("user_name"),
                                    Action = reader.GetString("action"),
                                    StudentId = reader.GetInt32("student_id"),
                                    StudentName = reader.GetString("student_name"),
                                    Subject = reader.IsDBNull(reader.GetOrdinal("subject")) ? "" : reader.GetString("subject"),
                                    OldValue = reader.IsDBNull(reader.GetOrdinal("old_value")) ? "" : reader.GetString("old_value"),
                                    NewValue = reader.IsDBNull(reader.GetOrdinal("new_value")) ? "" : reader.GetString("new_value"),
                                    Date = reader.GetDateTime("date"),
                                    Comment = reader.IsDBNull(reader.GetOrdinal("comment")) ? "" : reader.GetString("comment")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetLogsForPeriod: {ex.Message}");
            }
            return logs;
        }

        public bool UpdateStudyPlan(StudyPlan plan)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== UpdateStudyPlan: Plan ID={plan.Id}, Subjects={plan.Subjects.Count} ===");

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Обновляем план
                            string updateQuery = @"
                        UPDATE study_plans 
                        SET name = @name,
                            variant = @variant,
                            description = @description,
                            updated_at = NOW()
                        WHERE id = @id";

                            using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", plan.Id);
                                cmd.Parameters.AddWithValue("@name", plan.Name);
                                cmd.Parameters.AddWithValue("@variant", plan.Variant);
                                cmd.Parameters.AddWithValue("@description", plan.Description ?? "");
                                int rowsAffected = cmd.ExecuteNonQuery();
                                System.Diagnostics.Debug.WriteLine($"UpdateStudyPlan: Updated plan, rows affected: {rowsAffected}");
                            }

                            // Удаляем старые предметы
                            string deleteQuery = "DELETE FROM plan_subjects WHERE plan_id = @planId";
                            using (MySqlCommand cmd = new MySqlCommand(deleteQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@planId", plan.Id);
                                int deleted = cmd.ExecuteNonQuery();
                                System.Diagnostics.Debug.WriteLine($"UpdateStudyPlan: Deleted {deleted} old subjects");
                            }

                            // Добавляем новые предметы
                            int inserted = 0;
                            foreach (var subject in plan.Subjects)
                            {
                                // Определяем subject_id
                                int? subjectId = subject.SubjectId;
                                if (subjectId == null || subjectId == 0)
                                {
                                    string findSubjectQuery = "SELECT id FROM subjects WHERE name = @name";
                                    using (MySqlCommand findCmd = new MySqlCommand(findSubjectQuery, conn, transaction))
                                    {
                                        findCmd.Parameters.AddWithValue("@name", subject.SubjectName);
                                        var result = findCmd.ExecuteScalar();
                                        if (result != null && result != DBNull.Value)
                                            subjectId = Convert.ToInt32(result);
                                    }
                                }

                                string subjectQuery = @"
                            INSERT INTO plan_subjects 
                            (plan_id, subject_name, subject_id, grade, hours_per_week, difficulty, is_required, sort_order)
                            VALUES 
                            (@planId, @subjectName, @subjectId, @grade, @hours, @difficulty, @isRequired, @sortOrder)";

                                using (MySqlCommand cmd = new MySqlCommand(subjectQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@planId", plan.Id);
                                    cmd.Parameters.AddWithValue("@subjectName", subject.SubjectName);
                                    cmd.Parameters.AddWithValue("@subjectId", subjectId.HasValue ? (object)subjectId.Value : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@grade", subject.Grade);
                                    cmd.Parameters.AddWithValue("@hours", subject.HoursPerWeek);
                                    cmd.Parameters.AddWithValue("@difficulty", subject.Difficulty);
                                    cmd.Parameters.AddWithValue("@isRequired", subject.IsRequired);
                                    cmd.Parameters.AddWithValue("@sortOrder", subject.SortOrder);
                                    cmd.ExecuteNonQuery();
                                    inserted++;
                                }
                            }
                            System.Diagnostics.Debug.WriteLine($"UpdateStudyPlan: Inserted {inserted} subjects");

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine("=== UpdateStudyPlan SUCCESS ===");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"=== UpdateStudyPlan ERROR: {ex.Message} ===");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления учебного плана: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        // ============================================================
        // АВТОМАТИЧЕСКОЕ ДОБАВЛЕНИЕ В ШКАЛУ ТРУДНОСТИ
        // ============================================================

        public bool AddSubjectToDifficulty(string subjectName)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, есть ли уже такой предмет в шкале трудности
                    string checkQuery = "SELECT COUNT(*) FROM subject_difficulty WHERE subject_name = @subjectName";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@subjectName", subjectName);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());
                        if (count > 0) return true; // Уже существует
                    }

                    // Добавляем предмет в шкалу трудности со значениями по умолчанию
                    string insertQuery = @"
                INSERT INTO subject_difficulty 
                (subject_name, grade_5, grade_6, grade_7, grade_8, grade_9, grade_10, grade_11)
                VALUES 
                (@subjectName, 1, 1, 1, 1, 1, 1, 1)";

                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@subjectName", subjectName);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка добавления в шкалу трудности: {ex.Message}");
                return false;
            }
        }

        public bool UpdateSubjectInDifficulty(string oldName, string newName)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "UPDATE subject_difficulty SET subject_name = @newName WHERE subject_name = @oldName";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@newName", newName);
                        cmd.Parameters.AddWithValue("@oldName", oldName);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления в шкале трудности: {ex.Message}");
                return false;
            }
        }

        public bool DeleteSubjectFromDifficulty(string subjectName)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "DELETE FROM subject_difficulty WHERE subject_name = @subjectName";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@subjectName", subjectName);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления из шкалы трудности: {ex.Message}");
                return false;
            }
        }
        // ============================================================
        // УДАЛЕНИЕ ПРЕДМЕТА ИЗ БД
        // ============================================================

        public bool DeleteSubject(string subjectName)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, используется ли предмет в расписании
                    string checkQuery = "SELECT COUNT(*) FROM schedule_lessons WHERE subject = @subjectName";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@subjectName", subjectName);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            MessageBox.Show($"Предмет '{subjectName}' используется в расписании. Сначала удалите уроки с этим предметом.",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }
                    }

                    // Мягкое удаление (помечаем как неактивный)
                    string query = "UPDATE subjects SET is_active = FALSE WHERE name = @name";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", subjectName);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления предмета: {ex.Message}");
                return false;
            }
        }
        public StudyPlan GetPlanForClass(string className)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"GetPlanForClass: {className}");

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string getPlanIdQuery = "SELECT plan_id FROM class_plan WHERE class_name = @className";
                    int? planId = null;
                    using (MySqlCommand cmd = new MySqlCommand(getPlanIdQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@className", className);
                        var result = cmd.ExecuteScalar();
                        System.Diagnostics.Debug.WriteLine($"PlanId result: {result}");
                        if (result != null && result != DBNull.Value)
                            planId = Convert.ToInt32(result);
                    }

                    if (planId == null || planId == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"No plan for {className}");
                        return null;
                    }

                    string query = "SELECT * FROM study_plans WHERE id = @planId AND is_active = TRUE";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@planId", planId);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var plan = new StudyPlan
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("name"),
                                    Variant = reader.GetString("variant"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                                    IsActive = reader.GetBoolean("is_active"),
                                    CreatedBy = reader.GetInt32("created_by"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    UpdatedAt = reader.GetDateTime("updated_at")
                                };
                                reader.Close();
                                LoadPlanSubjects(plan);
                                System.Diagnostics.Debug.WriteLine($"Plan found: {plan.Name}, subjects: {plan.Subjects.Count}");
                                return plan;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetPlanForClass: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            }

            return null;
        }
        // ============================================================
        // ДОПОЛНИТЕЛЬНЫЕ МЕТОДЫ ДЛЯ ADMINPANELPAGE
        // ============================================================

        // Добавление пользователя (возвращает ID)
        // Добавление пользователя
        public int AddUser(string fullName, string username, string passwordHash, string role, string subject = null, int maxHours = 20)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                INSERT INTO users (full_name, username, password_hash, role, subject, subjects_list, max_hours_per_week)
                VALUES (@fullName, @username, @passwordHash, @role, @subject, @subject, @maxHours);
                SELECT LAST_INSERT_ID();";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@fullName", fullName);
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                        cmd.Parameters.AddWithValue("@role", role);
                        cmd.Parameters.AddWithValue("@subject", subject ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@maxHours", maxHours);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка AddUser: {ex.Message}");
                return -1;
            }
        }

        // Добавление учителя
        public bool AddTeacher(int userId, string subject, int maxHours, string room)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // ✅ ТОЛЬКО ОБНОВЛЯЕМ USERS
                    string query = @"
                UPDATE users 
                SET subject = @subject,
                    subjects_list = @subject,
                    max_hours_per_week = @maxHours,
                    room = @room
                WHERE id = @userId AND role = 'Teacher'";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@subject", subject ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@maxHours", maxHours);
                        cmd.Parameters.AddWithValue("@room", room ?? (object)DBNull.Value);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка AddTeacher: {ex.Message}");
                return false;
            }
        }

        // Обновление пароля пользователя
        public bool UpdateUser(int userId, string fullName, string username, string passwordHash, string subject, int maxHours)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем существование пользователя
                    string checkQuery = "SELECT COUNT(*) FROM users WHERE id = @userId";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@userId", userId);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());
                        if (count == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Пользователь с ID {userId} не найден!");
                            return false;
                        }
                    }

                    // Проверяем уникальность username
                    string checkUsernameQuery = "SELECT COUNT(*) FROM users WHERE username = @username AND id != @userId";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkUsernameQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@username", username);
                        checkCmd.Parameters.AddWithValue("@userId", userId);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());
                        if (count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ Username '{username}' уже занят!");
                            MessageBox.Show($"Логин '{username}' уже используется другим пользователем.",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }
                    }

                    string query;
                    if (string.IsNullOrEmpty(passwordHash))
                    {
                        query = @"
                    UPDATE users 
                    SET full_name = @fullName,
                        username = @username,
                        subject = @subject,
                        max_hours_per_week = @maxHours
                    WHERE id = @userId";
                    }
                    else
                    {
                        query = @"
                    UPDATE users 
                    SET full_name = @fullName,
                        username = @username,
                        password_hash = @passwordHash,
                        subject = @subject,
                        max_hours_per_week = @maxHours
                    WHERE id = @userId";
                    }

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@fullName", fullName);
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@subject", string.IsNullOrEmpty(subject) ? (object)DBNull.Value : subject);
                        cmd.Parameters.AddWithValue("@maxHours", maxHours);

                        if (!string.IsNullOrEmpty(passwordHash))
                        {
                            cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                        }

                        int result = cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"✅ UpdateUser: обновлено {result} записей для userId={userId}");
                        return result > 0;
                    }
                }
            }
            catch (MySqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ MySQL ошибка UpdateUser: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Код ошибки: {ex.Number}");
                MessageBox.Show($"Ошибка БД: {ex.Message}\nКод: {ex.Number}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка UpdateUser: {ex.Message}");
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }


        // Обновление учителя (переопределение с правильной сигнатурой)
        public bool UpdateTeacher(int userId, string fullName, string subject, string username, string password, int maxHours, string room)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Обновляем пользователя
                            string userQuery;
                            if (string.IsNullOrEmpty(password))
                            {
                                userQuery = @"
                            UPDATE users 
                            SET full_name = @fullName,
                                username = @username,
                                subject = @subject,
                                max_hours_per_week = @maxHours
                            WHERE id = @userId";
                            }
                            else
                            {
                                userQuery = @"
                            UPDATE users 
                            SET full_name = @fullName,
                                username = @username,
                                password_hash = @password,
                                subject = @subject,
                                max_hours_per_week = @maxHours
                            WHERE id = @userId";
                            }

                            using (MySqlCommand cmd = new MySqlCommand(userQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@userId", userId);
                                cmd.Parameters.AddWithValue("@fullName", fullName);
                                cmd.Parameters.AddWithValue("@username", username);
                                cmd.Parameters.AddWithValue("@subject", subject ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@maxHours", maxHours);
                                if (!string.IsNullOrEmpty(password))
                                {
                                    cmd.Parameters.AddWithValue("@password", password);
                                }
                                cmd.ExecuteNonQuery();
                            }

                            // 2. Обновляем учителя
                            string teacherQuery = @"
                        UPDATE teachers 
                        SET full_name = @fullName,
                            subject = @subject,
                            main_subject = @subject,
                            max_hours_per_week = @maxHours,
                            room = @room
                        WHERE user_id = @userId";

                            using (MySqlCommand cmd = new MySqlCommand(teacherQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@userId", userId);
                                cmd.Parameters.AddWithValue("@fullName", fullName);
                                cmd.Parameters.AddWithValue("@subject", subject ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@maxHours", maxHours);
                                cmd.Parameters.AddWithValue("@room", room ?? (object)DBNull.Value);
                                int result = cmd.ExecuteNonQuery();

                                // Если запись не найдена, создаем новую
                                if (result == 0)
                                {
                                    string insertQuery = @"
                                INSERT INTO teachers (user_id, full_name, subject, subjects_list, main_subject, max_hours_per_week, room)
                                VALUES (@userId, @fullName, @subject, @subject, @subject, @maxHours, @room)";

                                    using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn, transaction))
                                    {
                                        insertCmd.Parameters.AddWithValue("@userId", userId);
                                        insertCmd.Parameters.AddWithValue("@fullName", fullName);
                                        insertCmd.Parameters.AddWithValue("@subject", subject ?? (object)DBNull.Value);
                                        insertCmd.Parameters.AddWithValue("@maxHours", maxHours);
                                        insertCmd.Parameters.AddWithValue("@room", room ?? (object)DBNull.Value);
                                        insertCmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateTeacher: {ex.Message}");
                return false;
            }
        }

        public List<SubjectInfo> GetAllUniqueSubjects()
        {
            List<SubjectInfo> subjects = new List<SubjectInfo>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Получаем все уникальные предметы из обеих таблиц
                    string query = @"
                SELECT DISTINCT 
                    COALESCE(s.id, 0) as id,
                    COALESCE(s.name, ps.subject_name) as name,
                    COALESCE(s.hours_per_week, 2) as hours_per_week,
                    COALESCE(s.sort_order, 0) as sort_order
                FROM plan_subjects ps
                LEFT JOIN subjects s ON s.name = ps.subject_name
                UNION
                SELECT 
                    s.id,
                    s.name,
                    s.hours_per_week,
                    s.sort_order
                FROM subjects s
                WHERE s.is_active = TRUE
                ORDER BY sort_order, name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                subjects.Add(new SubjectInfo
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("name"),
                                    HoursPerWeek = reader.GetInt32("hours_per_week"),
                                    SortOrder = reader.IsDBNull(reader.GetOrdinal("sort_order")) ? (int?)null : reader.GetInt32("sort_order")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetAllUniqueSubjects: {ex.Message}");
            }

            return subjects;
        }

        // В DbHelper.cs - исправленный метод UpdateTeacherSubjects

        public bool UpdateTeacherSubjects(int userId, List<string> subjects)
        {
            try
            {
                string subjectsString = string.Join(", ", subjects);
                string mainSubject = subjects.FirstOrDefault() ?? "";

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // ✅ ТОЛЬКО USERS
                    string query = @"
                UPDATE users 
                SET subjects_list = @subjectsList,
                    subject = @mainSubject
                WHERE id = @userId AND role = 'Teacher'";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@subjectsList", subjectsString);
                        cmd.Parameters.AddWithValue("@mainSubject", mainSubject);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateTeacherSubjects: {ex.Message}");
                return false;
            }
        }
        public List<TeacherInfo> GetAllTeachersWithDetails()
        {
            List<TeacherInfo> teachers = new List<TeacherInfo>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // ✅ ТОЛЬКО ИЗ USERS, БЕЗ TEACHERS
                    string query = @"
                SELECT 
                    id as user_id,
                    full_name,
                    username,
                    subject,
                    subjects_list,
                    max_hours_per_week,
                    room
                FROM users 
                WHERE role = 'Teacher'
                ORDER BY full_name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string subjectValue = "";
                                if (!reader.IsDBNull(reader.GetOrdinal("subjects_list")))
                                {
                                    subjectValue = reader.GetString("subjects_list");
                                }
                                else if (!reader.IsDBNull(reader.GetOrdinal("subject")))
                                {
                                    subjectValue = reader.GetString("subject");
                                }

                                var teacher = new TeacherInfo
                                {
                                    Id = reader.GetInt32("user_id"),
                                    UserId = reader.GetInt32("user_id"),
                                    FullName = reader.GetString("full_name"),
                                    Username = reader.GetString("username"),
                                    Subject = subjectValue,
                                    SubjectsList = subjectValue,
                                    MaxHours = reader.IsDBNull(reader.GetOrdinal("max_hours_per_week")) ? 20 : reader.GetInt32("max_hours_per_week"),
                                    Room = reader.IsDBNull(reader.GetOrdinal("room")) ? "" : reader.GetString("room")
                                };

                                teachers.Add(teacher);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetAllTeachersWithDetails: {ex.Message}");
            }

            return teachers;
        }
        public int GetTeacherIdByUserId(int userId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, есть ли запись в teachers
                    string query = "SELECT id FROM teachers WHERE user_id = @userId";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            return Convert.ToInt32(result);

                        // Если записи нет - создаем
                        string getNameQuery = "SELECT full_name, subject, max_hours_per_week FROM users WHERE id = @userId";
                        string fullName = "";
                        string subject = "";
                        int maxHours = 20;

                        using (MySqlCommand getNameCmd = new MySqlCommand(getNameQuery, conn))
                        {
                            getNameCmd.Parameters.AddWithValue("@userId", userId);
                            using (MySqlDataReader reader = getNameCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    fullName = reader.GetString("full_name");
                                    subject = reader.IsDBNull(reader.GetOrdinal("subject")) ? "" : reader.GetString("subject");
                                    maxHours = reader.IsDBNull(reader.GetOrdinal("max_hours_per_week")) ? 20 : reader.GetInt32("max_hours_per_week");
                                }
                                reader.Close();
                            }
                        }

                        if (!string.IsNullOrEmpty(fullName))
                        {
                            string insertQuery = @"
                        INSERT INTO teachers (user_id, full_name, subject, subjects_list, main_subject, max_hours_per_week)
                        VALUES (@userId, @fullName, @subject, @subject, @subject, @maxHours);
                        SELECT LAST_INSERT_ID();";

                            using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn))
                            {
                                insertCmd.Parameters.AddWithValue("@userId", userId);
                                insertCmd.Parameters.AddWithValue("@fullName", fullName);
                                insertCmd.Parameters.AddWithValue("@subject", subject ?? (object)DBNull.Value);
                                insertCmd.Parameters.AddWithValue("@maxHours", maxHours);
                                int newId = Convert.ToInt32(insertCmd.ExecuteScalar());
                                return newId;
                            }
                        }

                        return -1;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetTeacherIdByUserId: {ex.Message}");
                return -1;
            }
        }
        // Добавьте этот метод в ваш DbHelper
        private void LoadPlanSubjects(StudyPlan plan)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT id, plan_id, subject_name, subject_id, grade, hours_per_week, difficulty, is_required, sort_order
                FROM plan_subjects 
                WHERE plan_id = @planId 
                ORDER BY sort_order";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@planId", plan.Id);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                plan.Subjects.Add(new PlanSubject
                                {
                                    Id = reader.GetInt32("id"),
                                    PlanId = reader.GetInt32("plan_id"),
                                    SubjectName = reader.GetString("subject_name"),
                                    SubjectId = reader.IsDBNull(reader.GetOrdinal("subject_id")) ? (int?)null : reader.GetInt32("subject_id"),
                                    Grade = reader.GetInt32("grade"),
                                    HoursPerWeek = reader.GetInt32("hours_per_week"),
                                    Difficulty = reader.GetInt32("difficulty"),
                                    IsRequired = reader.GetBoolean("is_required"),
                                    SortOrder = reader.GetInt32("sort_order")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadPlanSubjects: {ex.Message}");
            }
        }
        // ============================================================
        // МЕТОДЫ ДЛЯ УЧЕБНЫХ ПЛАНОВ
        // ============================================================

        public int SaveStudyPlan(StudyPlan plan)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== SaveStudyPlan START ===");
                System.Diagnostics.Debug.WriteLine($"Plan ID: {plan.Id}, Name: {plan.Name}, Subjects: {plan.Subjects.Count}");

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            int planId;

                            if (plan.Id == 0)
                            {
                                // Проверяем, существует ли уже план с таким именем
                                string checkQuery = "SELECT id FROM study_plans WHERE name = @name AND is_active = TRUE";
                                using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn, transaction))
                                {
                                    checkCmd.Parameters.AddWithValue("@name", plan.Name);
                                    var existingId = checkCmd.ExecuteScalar();
                                    if (existingId != null)
                                    {
                                        // План уже существует, обновляем его
                                        planId = Convert.ToInt32(existingId);
                                        plan.Id = planId;
                                        System.Diagnostics.Debug.WriteLine($"Plan exists with ID: {planId}, updating...");

                                        string updateQuery = @"
                                    UPDATE study_plans 
                                    SET variant = @variant,
                                        description = @description,
                                        updated_at = NOW()
                                    WHERE id = @id";

                                        using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@id", planId);
                                            cmd.Parameters.AddWithValue("@variant", plan.Variant);
                                            cmd.Parameters.AddWithValue("@description", plan.Description ?? "");
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                    else
                                    {
                                        // Создаем новый план
                                        string insertQuery = @"
                                    INSERT INTO study_plans (name, variant, description, created_by)
                                    VALUES (@name, @variant, @description, @createdBy);
                                    SELECT LAST_INSERT_ID();";

                                        using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@name", plan.Name);
                                            cmd.Parameters.AddWithValue("@variant", plan.Variant);
                                            cmd.Parameters.AddWithValue("@description", plan.Description ?? "");
                                            cmd.Parameters.AddWithValue("@createdBy", plan.CreatedBy);
                                            planId = Convert.ToInt32(cmd.ExecuteScalar());
                                            System.Diagnostics.Debug.WriteLine($"Created new plan with ID: {planId}");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Обновляем существующий план
                                planId = plan.Id;
                                System.Diagnostics.Debug.WriteLine($"Updating existing plan ID: {planId}");

                                string updateQuery = @"
                            UPDATE study_plans 
                            SET name = @name,
                                variant = @variant,
                                description = @description,
                                updated_at = NOW()
                            WHERE id = @id";

                                using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@id", plan.Id);
                                    cmd.Parameters.AddWithValue("@name", plan.Name);
                                    cmd.Parameters.AddWithValue("@variant", plan.Variant);
                                    cmd.Parameters.AddWithValue("@description", plan.Description ?? "");
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // Удаляем старые предметы
                            string deleteQuery = "DELETE FROM plan_subjects WHERE plan_id = @planId";
                            using (MySqlCommand cmd = new MySqlCommand(deleteQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@planId", planId);
                                cmd.ExecuteNonQuery();
                            }
                            System.Diagnostics.Debug.WriteLine($"Deleted old subjects for plan {planId}");

                            // Добавляем новые предметы
                            int subjectCount = 0;
                            foreach (var subject in plan.Subjects)
                            {
                                // Определяем subject_id
                                int? subjectId = subject.SubjectId;
                                if (subjectId == null || subjectId == 0)
                                {
                                    string findSubjectQuery = "SELECT id FROM subjects WHERE name = @name";
                                    using (MySqlCommand findCmd = new MySqlCommand(findSubjectQuery, conn, transaction))
                                    {
                                        findCmd.Parameters.AddWithValue("@name", subject.SubjectName);
                                        var result = findCmd.ExecuteScalar();
                                        if (result != null && result != DBNull.Value)
                                            subjectId = Convert.ToInt32(result);
                                    }
                                }

                                string subjectQuery = @"
                            INSERT INTO plan_subjects 
                            (plan_id, subject_name, subject_id, grade, hours_per_week, difficulty, is_required, sort_order)
                            VALUES 
                            (@planId, @subjectName, @subjectId, @grade, @hours, @difficulty, @isRequired, @sortOrder)";

                                using (MySqlCommand cmd = new MySqlCommand(subjectQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@planId", planId);
                                    cmd.Parameters.AddWithValue("@subjectName", subject.SubjectName);
                                    cmd.Parameters.AddWithValue("@subjectId", subjectId.HasValue ? (object)subjectId.Value : DBNull.Value);
                                    cmd.Parameters.AddWithValue("@grade", subject.Grade);
                                    cmd.Parameters.AddWithValue("@hours", subject.HoursPerWeek);
                                    cmd.Parameters.AddWithValue("@difficulty", subject.Difficulty);
                                    cmd.Parameters.AddWithValue("@isRequired", subject.IsRequired);
                                    cmd.Parameters.AddWithValue("@sortOrder", subject.SortOrder);
                                    cmd.ExecuteNonQuery();
                                    subjectCount++;
                                }
                            }
                            System.Diagnostics.Debug.WriteLine($"Added {subjectCount} subjects for plan {planId}");

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"=== SaveStudyPlan SUCCESS, Plan ID: {planId} ===");
                            return planId;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"=== SaveStudyPlan ERROR in transaction: {ex.Message} ===");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения учебного плана: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return -1;
            }
        }






        public List<StudyPlan> GetAllStudyPlans()
        {
            List<StudyPlan> plans = new List<StudyPlan>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT * FROM study_plans WHERE is_active = TRUE ORDER BY name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                plans.Add(new StudyPlan
                                {
                                    Id = reader.GetInt32("id"),
                                    Name = reader.GetString("name"),
                                    Variant = reader.GetString("variant"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                                    IsActive = reader.GetBoolean("is_active"),
                                    CreatedBy = reader.GetInt32("created_by"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    UpdatedAt = reader.GetDateTime("updated_at")
                                });
                            }
                        }
                    }

                    foreach (var plan in plans)
                    {
                        LoadPlanSubjects(plan);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки учебных планов: {ex.Message}");
            }

            return plans;
        }

        // ============================================================
        // МЕТОДЫ ДЛЯ РАБОТЫ С TEACHER_ROOMS
        // ============================================================

        /// <summary>
        /// Получить все закрепления кабинетов за учителями
        /// </summary>
        public List<TeacherRoom> GetAllTeacherRooms()
        {
            List<TeacherRoom> teacherRooms = new List<TeacherRoom>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    tr.*,
                    u.full_name as teacher_name,
                    r.number as room_number
                FROM teacher_rooms tr
                JOIN users u ON tr.teacher_id = u.id
                JOIN rooms r ON tr.room_id = r.id
                ORDER BY u.full_name, r.number";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                teacherRooms.Add(new TeacherRoom
                                {
                                    Id = reader.GetInt32("id"),
                                    TeacherId = reader.GetInt32("teacher_id"),
                                    RoomId = reader.GetInt32("room_id"),
                                    IsPrimary = reader.GetBoolean("is_primary"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    UpdatedAt = reader.GetDateTime("updated_at"),
                                    TeacherName = reader.GetString("teacher_name"),
                                    RoomNumber = reader.GetString("room_number")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetAllTeacherRooms: {ex.Message}");
            }

            return teacherRooms;
        }

        /// <summary>
        /// Получить кабинеты для учителя
        /// </summary>
        public List<TeacherRoom> GetRoomsForTeacher(int teacherId)
        {
            List<TeacherRoom> rooms = new List<TeacherRoom>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    tr.*,
                    r.number as room_number
                FROM teacher_rooms tr
                JOIN rooms r ON tr.room_id = r.id
                WHERE tr.teacher_id = @teacherId
                ORDER BY tr.is_primary DESC, r.number";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                rooms.Add(new TeacherRoom
                                {
                                    Id = reader.GetInt32("id"),
                                    TeacherId = reader.GetInt32("teacher_id"),
                                    RoomId = reader.GetInt32("room_id"),
                                    IsPrimary = reader.GetBoolean("is_primary"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    UpdatedAt = reader.GetDateTime("updated_at"),
                                    RoomNumber = reader.GetString("room_number")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetRoomsForTeacher: {ex.Message}");
            }

            return rooms;
        }

        /// <summary>
        /// Получить учителей для кабинета
        /// </summary>
        public List<TeacherRoom> GetTeachersForRoom(int roomId)
        {
            List<TeacherRoom> teachers = new List<TeacherRoom>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    tr.*,
                    u.full_name as teacher_name
                FROM teacher_rooms tr
                JOIN users u ON tr.teacher_id = u.id
                WHERE tr.room_id = @roomId
                ORDER BY tr.is_primary DESC, u.full_name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@roomId", roomId);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                teachers.Add(new TeacherRoom
                                {
                                    Id = reader.GetInt32("id"),
                                    TeacherId = reader.GetInt32("teacher_id"),
                                    RoomId = reader.GetInt32("room_id"),
                                    IsPrimary = reader.GetBoolean("is_primary"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    UpdatedAt = reader.GetDateTime("updated_at"),
                                    TeacherName = reader.GetString("teacher_name")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetTeachersForRoom: {ex.Message}");
            }

            return teachers;
        }

        /// <summary>
        /// Закрепить кабинет за учителем
        /// </summary>
        public bool AssignRoomToTeacher(int teacherId, int roomId, bool isPrimary = false)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, есть ли уже такая связь
                    string checkQuery = "SELECT id FROM teacher_rooms WHERE teacher_id = @teacherId AND room_id = @roomId";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@teacherId", teacherId);
                        checkCmd.Parameters.AddWithValue("@roomId", roomId);
                        var existing = checkCmd.ExecuteScalar();

                        if (existing != null && existing != DBNull.Value)
                        {
                            // Обновляем is_primary
                            string updateQuery = "UPDATE teacher_rooms SET is_primary = @isPrimary WHERE id = @id";
                            using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@id", existing);
                                cmd.Parameters.AddWithValue("@isPrimary", isPrimary);
                                return cmd.ExecuteNonQuery() > 0;
                            }
                        }

                        // Если нужно установить основной кабинет, сбрасываем предыдущий
                        if (isPrimary)
                        {
                            string resetPrimaryQuery = "UPDATE teacher_rooms SET is_primary = 0 WHERE teacher_id = @teacherId";
                            using (MySqlCommand cmd = new MySqlCommand(resetPrimaryQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@teacherId", teacherId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Создаем новую связь
                        string insertQuery = @"
                    INSERT INTO teacher_rooms (teacher_id, room_id, is_primary)
                    VALUES (@teacherId, @roomId, @isPrimary)";

                        using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@teacherId", teacherId);
                            cmd.Parameters.AddWithValue("@roomId", roomId);
                            cmd.Parameters.AddWithValue("@isPrimary", isPrimary);
                            return cmd.ExecuteNonQuery() > 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка AssignRoomToTeacher: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Удалить закрепление кабинета за учителем
        /// </summary>
        public bool UnassignRoomFromTeacher(int teacherId, int roomId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "DELETE FROM teacher_rooms WHERE teacher_id = @teacherId AND room_id = @roomId";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        cmd.Parameters.AddWithValue("@roomId", roomId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UnassignRoomFromTeacher: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получить основной кабинет учителя (автоматически подставляется в расписание)
        /// </summary>
        public string GetPrimaryRoomForTeacher(int teacherId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT r.number 
                FROM teacher_rooms tr
                JOIN rooms r ON tr.room_id = r.id
                WHERE tr.teacher_id = @teacherId AND tr.is_primary = 1
                LIMIT 1";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        var result = cmd.ExecuteScalar();
                        return result?.ToString() ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetPrimaryRoomForTeacher: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Обновить TeacherInfo с кабинетами
        /// </summary>
        public TeacherInfo GetTeacherWithRooms(int userId)
        {
            try
            {
                var teacher = GetAllTeachersWithDetails().FirstOrDefault(t => t.UserId == userId);
                if (teacher != null)
                {
                    teacher.Rooms = GetRoomsForTeacher(userId);
                    teacher.Room = teacher.Rooms.FirstOrDefault(r => r.IsPrimary)?.RoomNumber ??
                                   teacher.Rooms.FirstOrDefault()?.RoomNumber ?? "";
                }
                return teacher;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetTeacherWithRooms: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Автоматическое заполнение кабинета при создании урока
        /// </summary>
        public string GetRoomForTeacher(int teacherId, string subject = null)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // ✅ ИЩЕМ КАБИНЕТ В USERS
                    string query = "SELECT room FROM users WHERE id = @teacherId AND role = 'Teacher'";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            return result.ToString();
                    }

                    return "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetRoomForTeacher: {ex.Message}");
                return "";
            }
        }
        public User GetUserById(int userId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT id, full_name, username, role, subject, max_hours_per_week FROM users WHERE id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", userId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new User
                                {
                                    Id = reader.GetInt32("id"),
                                    FullName = reader.GetString("full_name"),
                                    Username = reader.GetString("username"),
                                    Role = (UserRole)Enum.Parse(typeof(UserRole), reader.GetString("role")),
                                    Subject = reader.IsDBNull(reader.GetOrdinal("subject")) ? null : reader.GetString("subject"),
                                    MaxHoursPerWeek = reader.IsDBNull(reader.GetOrdinal("max_hours_per_week")) ? 20 : reader.GetInt32("max_hours_per_week")
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetUserById: {ex.Message}");
            }
            return null;
        }
        public bool UpdateTeacherFull(int userId, string fullName, string username, string passwordHash,
                                  List<string> subjects, int maxHours, string room)
        {
            try
            {
                string subjectsString = string.Join(", ", subjects);
                string mainSubject = subjects.FirstOrDefault() ?? "";

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // ✅ ТОЛЬКО USERS
                            string userQuery;
                            if (string.IsNullOrEmpty(passwordHash))
                            {
                                userQuery = @"
                            UPDATE users 
                            SET full_name = @fullName,
                                username = @username,
                                subject = @mainSubject,
                                subjects_list = @subjectsList,
                                max_hours_per_week = @maxHours,
                                room = @room
                            WHERE id = @userId AND role = 'Teacher'";
                            }
                            else
                            {
                                userQuery = @"
                            UPDATE users 
                            SET full_name = @fullName,
                                username = @username,
                                password_hash = @passwordHash,
                                subject = @mainSubject,
                                subjects_list = @subjectsList,
                                max_hours_per_week = @maxHours,
                                room = @room
                            WHERE id = @userId AND role = 'Teacher'";
                            }

                            using (MySqlCommand cmd = new MySqlCommand(userQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@userId", userId);
                                cmd.Parameters.AddWithValue("@fullName", fullName);
                                cmd.Parameters.AddWithValue("@username", username);
                                cmd.Parameters.AddWithValue("@mainSubject", mainSubject);
                                cmd.Parameters.AddWithValue("@subjectsList", subjectsString);
                                cmd.Parameters.AddWithValue("@maxHours", maxHours);
                                cmd.Parameters.AddWithValue("@room", string.IsNullOrEmpty(room) ? (object)DBNull.Value : room);

                                if (!string.IsNullOrEmpty(passwordHash))
                                {
                                    cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                                }

                                int result = cmd.ExecuteNonQuery();

                                if (result > 0)
                                {
                                    transaction.Commit();
                                    return true;
                                }
                                else
                                {
                                    transaction.Rollback();
                                    return false;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateTeacherFull: {ex.Message}");
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }


        // ============================================================
        // ПРЕДМЕТЫ УЧИТЕЛЯ ПО КЛАССАМ
        // ============================================================

        /// <summary>
        /// Добавить предмет учителю в классе
        /// </summary>
        public bool AddTeacherSubject(int teacherId, int subjectId, int classId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                INSERT IGNORE INTO teacher_subjects (teacher_id, subject_id, class_id)
                VALUES (@teacherId, @subjectId, @classId)";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        cmd.Parameters.AddWithValue("@subjectId", subjectId);
                        cmd.Parameters.AddWithValue("@classId", classId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка AddTeacherSubject: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Удалить предмет учителя в классе
        /// </summary>
        public bool RemoveTeacherSubject(int teacherId, int subjectId, int classId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                DELETE FROM teacher_subjects 
                WHERE teacher_id = @teacherId 
                AND subject_id = @subjectId 
                AND class_id = @classId";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        cmd.Parameters.AddWithValue("@subjectId", subjectId);
                        cmd.Parameters.AddWithValue("@classId", classId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка RemoveTeacherSubject: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получить все предметы учителя по классам
        /// </summary>
        public Dictionary<string, List<string>> GetTeacherSubjectsByClass(int teacherId)
        {
            var result = new Dictionary<string, List<string>>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    s.name as subject_name,
                    c.name as class_name
                FROM teacher_subjects ts
                JOIN subjects s ON ts.subject_id = s.id
                JOIN classes c ON ts.class_id = c.id
                WHERE ts.teacher_id = @teacherId
                ORDER BY c.name, s.name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string subject = reader.GetString("subject_name");
                                string className = reader.GetString("class_name");

                                if (!result.ContainsKey(className))
                                    result[className] = new List<string>();

                                if (!result[className].Contains(subject))
                                    result[className].Add(subject);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetTeacherSubjectsByClass: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Получить все предметы учителя (уникальные)
        /// </summary>


        public List<string> GetTeacherClasses(int teacherId)
        {
            var classes = new List<string>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT DISTINCT c.name
                FROM teacher_subjects ts
                JOIN classes c ON ts.class_id = c.id
                WHERE ts.teacher_id = @teacherId
                ORDER BY c.name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                classes.Add(reader.GetString("name"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetTeacherClasses: {ex.Message}");
            }

            return classes;
        }

        /// <summary>
        /// Сохранить все предметы учителя по классам (удаляет старые и добавляет новые)
        /// </summary>
        public bool SaveTeacherSubjects(int teacherId, Dictionary<int, List<int>> subjectsByClass)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Удаляем все старые записи
                            string deleteQuery = "DELETE FROM teacher_subjects WHERE teacher_id = @teacherId";
                            using (MySqlCommand deleteCmd = new MySqlCommand(deleteQuery, conn, transaction))
                            {
                                deleteCmd.Parameters.AddWithValue("@teacherId", teacherId);
                                deleteCmd.ExecuteNonQuery();
                            }

                            // Добавляем новые записи
                            int added = 0;
                            foreach (var kvp in subjectsByClass)
                            {
                                int classId = kvp.Key;
                                foreach (int subjectId in kvp.Value)
                                {
                                    string insertQuery = @"
                                INSERT INTO teacher_subjects (teacher_id, subject_id, class_id)
                                VALUES (@teacherId, @subjectId, @classId)";

                                    using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn, transaction))
                                    {
                                        insertCmd.Parameters.AddWithValue("@teacherId", teacherId);
                                        insertCmd.Parameters.AddWithValue("@subjectId", subjectId);
                                        insertCmd.Parameters.AddWithValue("@classId", classId);
                                        insertCmd.ExecuteNonQuery();
                                        added++;
                                    }
                                }
                            }

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"Сохранено {added} записей для учителя {teacherId}");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"Ошибка SaveTeacherSubjects: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SaveTeacherSubjects: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Проверить, есть ли у учителя предмет в классе
        /// </summary>
        public bool HasTeacherSubject(int teacherId, int subjectId, int classId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT COUNT(*) FROM teacher_subjects 
                WHERE teacher_id = @teacherId 
                AND subject_id = @subjectId 
                AND class_id = @classId";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        cmd.Parameters.AddWithValue("@subjectId", subjectId);
                        cmd.Parameters.AddWithValue("@classId", classId);
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка HasTeacherSubject: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получить всех учителей для предмета в классе
        /// </summary>
        public List<TeacherInfo> GetTeachersForSubjectInClass(int subjectId, int classId)
        {
            var teachers = new List<TeacherInfo>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                SELECT u.id, u.full_name, u.subject, u.max_hours_per_week, u.room
                FROM teacher_subjects ts
                JOIN users u ON ts.teacher_id = u.id
                WHERE ts.subject_id = @subjectId 
                AND ts.class_id = @classId
                AND u.role = 'Teacher'
                ORDER BY u.full_name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@subjectId", subjectId);
                        cmd.Parameters.AddWithValue("@classId", classId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                teachers.Add(new TeacherInfo
                                {
                                    Id = reader.GetInt32("id"),
                                    FullName = reader.GetString("full_name"),
                                    Subject = reader.IsDBNull(reader.GetOrdinal("subject")) ? "" : reader.GetString("subject"),
                                    MaxHours = reader.IsDBNull(reader.GetOrdinal("max_hours_per_week")) ? 20 : reader.GetInt32("max_hours_per_week"),
                                    Room = reader.IsDBNull(reader.GetOrdinal("room")) ? "" : reader.GetString("room")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetTeachersForSubjectInClass: {ex.Message}");
            }

            return teachers;
        }


        // ============================================================
        // ВЫХОДНЫЕ ДНИ УЧИТЕЛЕЙ (ГРАФИК РАБОТЫ)
        // ============================================================

        /// <summary>
        /// Получить выходные дни учителя
        /// </summary>
        public List<string> GetTeacherDaysOff(int teacherId)
        {
            var daysOff = new List<string>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT day_of_week FROM teacher_days_off WHERE teacher_id = @teacherId";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                daysOff.Add(reader.GetString("day_of_week"));
                                System.Diagnostics.Debug.WriteLine($"Найден день: {reader.GetString("day_of_week")} для teacher_id={teacherId}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetTeacherDaysOff: {ex.Message}");
            }

            return daysOff;
        }

        /// <summary>
        /// Сохранить выходные дни учителя
        /// </summary>
        public bool SaveTeacherDaysOff(int teacherId, bool monday, bool tuesday, bool wednesday, bool thursday, bool friday)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Удаляем старые записи
                    string deleteQuery = "DELETE FROM teacher_days_off WHERE teacher_id = @teacherId";
                    using (MySqlCommand deleteCmd = new MySqlCommand(deleteQuery, conn))
                    {
                        deleteCmd.Parameters.AddWithValue("@teacherId", teacherId);
                        int deleted = deleteCmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"Удалено старых записей: {deleted} для teacher_id={teacherId}");
                    }

                    // Добавляем новые
                    var days = new List<string>();
                    if (monday) days.Add("Monday");
                    if (tuesday) days.Add("Tuesday");
                    if (wednesday) days.Add("Wednesday");
                    if (thursday) days.Add("Thursday");
                    if (friday) days.Add("Friday");

                    if (!days.Any())
                    {
                        System.Diagnostics.Debug.WriteLine($"Нет дней для сохранения для teacher_id={teacherId}");
                        return true;
                    }

                    int inserted = 0;
                    foreach (var day in days)
                    {
                        string insertQuery = @"
                    INSERT INTO teacher_days_off (teacher_id, day_of_week)
                    VALUES (@teacherId, @dayOfWeek)";

                        using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@teacherId", teacherId);
                            insertCmd.Parameters.AddWithValue("@dayOfWeek", day);
                            int result = insertCmd.ExecuteNonQuery();
                            if (result > 0) inserted++;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Сохранено дней: {inserted} для teacher_id={teacherId}");
                    return inserted == days.Count;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SaveTeacherDaysOff: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Проверить, работает ли учитель в день
        /// </summary>
        public bool IsTeacherWorking(int teacherId, string dayOfWeek)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT COUNT(*) FROM teacher_days_off WHERE teacher_id = @teacherId AND day_of_week = @dayOfWeek";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@teacherId", teacherId);
                        cmd.Parameters.AddWithValue("@dayOfWeek", dayOfWeek);
                        return Convert.ToInt32(cmd.ExecuteScalar()) == 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка IsTeacherWorking: {ex.Message}");
                return true;
            }
        }

    }
}
