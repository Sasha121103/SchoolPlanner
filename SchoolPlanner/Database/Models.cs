using System;
using System.Collections.Generic;

namespace SchoolPlanner.Database
{


    public enum LessonStatus
    {
        Draft,
        Pending,
        Approved,
        RequiresRevision
    }

    public enum ScheduleStatus
    {
        Draft,
        Pending,
        Approved,
        RequiresCorrection
    }

    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public UserRole Role { get; set; }
        public string Subject { get; set; }  // ← Добавлено
        public int MaxHoursPerWeek { get; set; } = 20;  // ← Добавлено
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public enum UserRole
    {
        Admin,
        Teacher
    }
    public class LessonPlan
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Subject { get; set; }
        public string Class { get; set; }
        public string Goal { get; set; }
        public List<string> Tasks { get; set; } = new List<string>();
        public List<LessonStage> Stages { get; set; } = new List<LessonStage>();
        public int TeacherId { get; set; }
        public string TeacherName { get; set; }
        public DateTime CreatedDate { get; set; }
        public LessonStatus Status { get; set; }
        public List<Comment> Comments { get; set; } = new List<Comment>();
        public List<string> AttachedFiles { get; set; } = new List<string>();
        public bool IsDeleted { get; internal set; }
        public int? ReviewedBy { get; internal set; }
        public DateTime? ReviewedDate { get; internal set; }
        public int ClassId { get; internal set; }
        public int SubjectId { get; internal set; }
    }

    public class LessonStage
    {
        public string Name { get; set; }
        public int Duration { get; set; }
        public string Description { get; set; }
        public string Example { get; set; }
    }

    public class Comment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string Text { get; set; }
        public DateTime Date { get; set; }
    }

    public class SchoolClass
    {
        public int Id { get; set; }
        public string Name { get; set; } // 5а, 6б и т.д.
        public int StudentsCount { get; set; }
    }

    public class Subject
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int DurationMinutes { get; set; } = 45;
        public int WeeklyFrequency { get; set; }
        public List<string> Classes { get; set; } = new List<string>();
    }

    public class Teacher
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Subject { get; set; }
        public int MaxHoursPerWeek { get; set; }
        public string Room { get; set; }  // ✅ Добавлено
        public List<string> Classes { get; set; } = new List<string>();
    }

    public class ScheduleLesson
    {
        public int Id { get; set; }
        public string Subject { get; set; }
        public string Class { get; set; }
        public string Teacher { get; set; }
        public int TeacherId { get; set; }
        public string DayOfWeek { get; set; }
        public int LessonNumber { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Room { get; set; }
        public int? LessonPlanId { get; set; }
        public string LessonPlanTitle { get; set; }
        public string Homework { get; set; }
        public string Note { get; set; }
        public bool IsCanceled { get; internal set; }
        public int ClassId { get; internal set; }
        public int SubjectId { get; internal set; }
        public int ScheduleId { get; internal set; }
    }

    public class Schedule
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public ScheduleStatus Status { get; set; }
        public List<ScheduleLesson> Lessons { get; set; } = new List<ScheduleLesson>();
        public List<Comment> Comments { get; set; } = new List<Comment>();
        public DateTime CreatedAt { get; internal set; }
        public bool IsActive { get; internal set; }
        public int CreatedBy { get; internal set; }
        public int? ApprovedBy { get; internal set; }
        public DateTime? ApprovedDate { get; internal set; }
    }

    public class FgosTemplate
    {
        public int Id { get; set; }
        public string Subject { get; set; }
        public string Grade { get; set; }
        public string Topic { get; set; }
        public string Goal { get; set; }
        public List<string> Tasks { get; set; }
        public List<LessonStage> Stages { get; set; }
        public string Source { get; set; } // edsoo.ru, prosv.ru и т.д.
        public int SubjectId { get; internal set; }
    }
    public enum AttendanceStatus
    {
        Present,    // Присутствовал
        Absent,     // Отсутствовал
        Late,       // Опоздал
        Excused     // Уважительная причина
    }

    public class Grade
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string Subject { get; set; }
        public int Value { get; set; }
        public DateTime Date { get; set; }
        public string Comment { get; set; }
        public string LessonTitle { get; set; }
    }

    public class Attendance
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public DateTime Date { get; set; }
        public AttendanceStatus Status { get; set; }
        public string Note { get; set; }
        public string LessonTitle { get; set; }
        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case AttendanceStatus.Present: return "Присутствовал";
                    case AttendanceStatus.Absent: return "Отсутствовал";
                    case AttendanceStatus.Late: return "Опоздал";
                    case AttendanceStatus.Excused: return "Уважительная причина";
                    default: return Status.ToString();
                }
            }
        }

        public class LogEntry
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string UserName { get; set; }
            public string Action { get; set; }
            public string ActionText { get; set; }
            public int StudentId { get; set; }
            public string StudentName { get; set; }
            public string Subject { get; set; }
            public string OldValue { get; set; }
            public string NewValue { get; set; }
            public DateTime Date { get; set; }
            public string Comment { get; set; }
        }
    }
}
