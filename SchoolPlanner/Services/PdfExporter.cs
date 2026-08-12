using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using SchoolPlanner.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace SchoolPlanner.Services
{
    public class PdfExporter
    {
        private BaseFont baseFont;
        private Font titleFont;
        private Font headerFont;
        private Font cellFont;
        private Font smallFont;
        private Font boldCellFont;
        private Font normalFont;
        private Font italicFont;
        private Font boldFont;
        private Font subjectFont;
        private Font teacherFont;
        private Font roomFont;

        public PdfExporter()
        {
            try
            {
                baseFont = BaseFont.CreateFont("c:/windows/fonts/arial.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
            }
            catch
            {
                try
                {
                    baseFont = BaseFont.CreateFont("c:/windows/fonts/times.ttf", BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                }
                catch
                {
                    try
                    {
                        baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.WINANSI, BaseFont.EMBEDDED);
                    }
                    catch
                    {
                        baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.WINANSI, BaseFont.NOT_EMBEDDED);
                    }
                }
            }

            titleFont = new Font(baseFont, 18, Font.BOLD);
            headerFont = new Font(baseFont, 12, Font.BOLD);
            cellFont = new Font(baseFont, 8, Font.NORMAL);
            smallFont = new Font(baseFont, 7, Font.NORMAL);
            boldCellFont = new Font(baseFont, 9, Font.BOLD);
            normalFont = new Font(baseFont, 12, Font.NORMAL);
            italicFont = new Font(baseFont, 11, Font.ITALIC);
            boldFont = new Font(baseFont, 12, Font.BOLD);

            // Дополнительные шрифты для расписания
            subjectFont = new Font(baseFont, 8, Font.BOLD);
            teacherFont = new Font(baseFont, 7, Font.NORMAL);
            roomFont = new Font(baseFont, 7, Font.NORMAL);
            roomFont.Color = BaseColor.GRAY;
        }

        public void ExportLessonPlan(LessonPlan plan)
        {
            if (plan == null)
            {
                MessageBox.Show("Нет данных для экспорта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = $"План_урока_{plan.Class}_{plan.Subject}_{plan.Title}_{plan.CreatedDate:yyyyMMdd}.pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    Document doc = new Document(PageSize.A4);
                    PdfWriter.GetInstance(doc, new FileStream(saveDialog.FileName, FileMode.Create));
                    doc.Open();

                    Paragraph title = new Paragraph("ПЛАН УРОКА", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    doc.Add(title);

                    doc.Add(new Paragraph(" ", new Font(baseFont, 2, Font.NORMAL)));
                    PdfPTable lineTable = new PdfPTable(1);
                    lineTable.WidthPercentage = 100;
                    PdfPCell lineCell = new PdfPCell();
                    lineCell.Border = Rectangle.BOTTOM_BORDER;
                    lineCell.BorderWidth = 2f;
                    lineCell.Padding = 0;
                    lineTable.AddCell(lineCell);
                    doc.Add(lineTable);
                    doc.Add(new Paragraph(" ", new Font(baseFont, 4, Font.NORMAL)));

                    PdfPTable infoTable = new PdfPTable(2);
                    infoTable.WidthPercentage = 100;
                    infoTable.SetWidths(new float[] { 0.25f, 0.75f });
                    infoTable.SpacingBefore = 5f;
                    infoTable.SpacingAfter = 10f;

                    AddInfoRow(infoTable, "📚 Предмет:", SafeString(plan.Subject), boldFont, normalFont);
                    AddInfoRow(infoTable, "🏫 Класс:", SafeString(plan.Class), boldFont, normalFont);
                    AddInfoRow(infoTable, "📖 Тема:", SafeString(plan.Title), boldFont, normalFont);
                    AddInfoRow(infoTable, "👨‍🏫 Учитель:", SafeString(plan.TeacherName), boldFont, normalFont);
                    AddInfoRow(infoTable, "📅 Дата:", plan.CreatedDate.ToString("dd.MM.yyyy"), boldFont, normalFont);
                    AddInfoRow(infoTable, "📌 Статус:", GetStatusText(plan.Status), boldFont, normalFont);

                    doc.Add(infoTable);

                    doc.Add(new Paragraph("\n🎯 Цель урока:", boldFont));
                    doc.Add(new Paragraph(SafeString(plan.Goal), normalFont));
                    doc.Add(new Paragraph(" ", new Font(baseFont, 4, Font.NORMAL)));

                    doc.Add(new Paragraph("📋 Задачи:", boldFont));
                    if (plan.Tasks != null && plan.Tasks.Any())
                    {
                        foreach (var task in plan.Tasks)
                        {
                            doc.Add(new Paragraph($"  • {SafeString(task)}", normalFont));
                        }
                    }
                    else
                    {
                        doc.Add(new Paragraph("  • Нет задач", normalFont));
                    }
                    doc.Add(new Paragraph(" ", new Font(baseFont, 4, Font.NORMAL)));

                    doc.Add(new Paragraph("📊 Этапы урока:", boldFont));
                    if (plan.Stages != null && plan.Stages.Any())
                    {
                        PdfPTable stageTable = new PdfPTable(2);
                        stageTable.WidthPercentage = 100;
                        stageTable.SetWidths(new float[] { 0.7f, 0.3f });
                        stageTable.SpacingBefore = 5f;

                        PdfPCell stageHeader = new PdfPCell(new Phrase("Название", headerFont));
                        stageHeader.BackgroundColor = new BaseColor(33, 150, 243);
                        stageHeader.Padding = 5;
                        stageTable.AddCell(stageHeader);

                        PdfPCell durationHeader = new PdfPCell(new Phrase("Время (мин)", headerFont));
                        durationHeader.BackgroundColor = new BaseColor(33, 150, 243);
                        durationHeader.Padding = 5;
                        stageTable.AddCell(durationHeader);

                        foreach (var stage in plan.Stages)
                        {
                            stageTable.AddCell(new PdfPCell(new Phrase(SafeString(stage.Name), normalFont)));
                            stageTable.AddCell(new PdfPCell(new Phrase(stage.Duration.ToString(), normalFont)));
                        }

                        doc.Add(stageTable);

                        doc.Add(new Paragraph("\nОписание этапов:", boldFont));
                        int stageIndex = 1;
                        foreach (var stage in plan.Stages)
                        {
                            doc.Add(new Paragraph($"{stageIndex}. {SafeString(stage.Name)}:", boldFont));
                            doc.Add(new Paragraph($"   {SafeString(stage.Description)}", normalFont));
                            if (!string.IsNullOrEmpty(stage.Example))
                            {
                                doc.Add(new Paragraph($"   Пример: {SafeString(stage.Example)}", italicFont));
                            }
                            doc.Add(new Paragraph(" ", new Font(baseFont, 2, Font.NORMAL)));
                            stageIndex++;
                        }
                    }
                    else
                    {
                        doc.Add(new Paragraph("  • Нет этапов", normalFont));
                    }

                    doc.Add(new Paragraph(" ", new Font(baseFont, 10, Font.NORMAL)));
                    PdfPTable footerTable = new PdfPTable(1);
                    footerTable.WidthPercentage = 100;
                    PdfPCell footerCell = new PdfPCell(new Phrase(
                        $"Создано: {DateTime.Now:dd.MM.yyyy HH:mm}",
                        new Font(baseFont, 8, Font.NORMAL, BaseColor.GRAY)
                    ));
                    footerCell.Border = Rectangle.TOP_BORDER;
                    footerCell.BorderWidth = 1f;
                    footerCell.Padding = 5;
                    footerCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    footerTable.AddCell(footerCell);
                    doc.Add(footerTable);

                    doc.Close();
                    MessageBox.Show("План урока успешно экспортирован в PDF", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта PDF: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddInfoRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            PdfPCell labelCell = new PdfPCell(new Phrase(label, labelFont));
            labelCell.Border = Rectangle.NO_BORDER;
            labelCell.Padding = 3;
            labelCell.BackgroundColor = new BaseColor(245, 245, 245);
            labelCell.HorizontalAlignment = Element.ALIGN_RIGHT;
            table.AddCell(labelCell);

            PdfPCell valueCell = new PdfPCell(new Phrase(value, valueFont));
            valueCell.Border = Rectangle.NO_BORDER;
            valueCell.Padding = 3;
            valueCell.HorizontalAlignment = Element.ALIGN_LEFT;
            table.AddCell(valueCell);
        }

        public void ExportSchedule(Schedule schedule)
        {
            if (schedule == null || schedule.Lessons == null || !schedule.Lessons.Any())
            {
                MessageBox.Show("Нет данных для экспорта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = $"Расписание_{schedule.Name}_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    Document doc = new Document(PageSize.A4.Rotate(), 20, 20, 15, 15);
                    PdfWriter.GetInstance(doc, new FileStream(saveDialog.FileName, FileMode.Create));
                    doc.Open();

                    // Заголовок
                    Paragraph title = new Paragraph("РАСПИСАНИЕ УРОКОВ", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingAfter = 5f;
                    doc.Add(title);

                    Paragraph subtitle = new Paragraph(SafeString(schedule.Name), new Font(baseFont, 14, Font.BOLD));
                    subtitle.Alignment = Element.ALIGN_CENTER;
                    subtitle.SpacingAfter = 3f;
                    doc.Add(subtitle);

                    Paragraph info = new Paragraph(
                        $"Период: {schedule.StartDate:dd.MM.yyyy} — {schedule.EndDate:dd.MM.yyyy}",
                        new Font(baseFont, 10, Font.NORMAL)
                    );
                    info.Alignment = Element.ALIGN_CENTER;
                    info.SpacingAfter = 10f;
                    doc.Add(info);

                    // Декоративная линия
                    PdfPTable lineTable = new PdfPTable(1);
                    lineTable.WidthPercentage = 80;
                    lineTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    PdfPCell lineCell = new PdfPCell();
                    lineCell.Border = Rectangle.BOTTOM_BORDER;
                    lineCell.BorderWidth = 2f;
                    lineCell.BorderColor = new BaseColor(33, 150, 243);
                    lineCell.Padding = 0;
                    lineTable.AddCell(lineCell);
                    doc.Add(lineTable);
                    doc.Add(new Paragraph(" ", new Font(baseFont, 6, Font.NORMAL)));

                    var classGroups = schedule.Lessons
                        .GroupBy(l => l.Class ?? "Без класса")
                        .OrderBy(g => g.Key)
                        .ToList();

                    if (!classGroups.Any())
                    {
                        doc.Add(new Paragraph("Нет уроков для отображения", new Font(baseFont, 14, Font.BOLD)));
                        doc.Close();
                        MessageBox.Show("В расписании нет уроков", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    for (int classIndex = 0; classIndex < classGroups.Count; classIndex++)
                    {
                        var classGroup = classGroups[classIndex];
                        string className = classGroup.Key;
                        var lessons = classGroup.ToList();

                        if (classIndex > 0)
                        {
                            doc.NewPage();
                        }

                        // Заголовок класса
                        PdfPTable classTitleTable = new PdfPTable(1);
                        classTitleTable.WidthPercentage = 100;
                        PdfPCell classTitleCell = new PdfPCell(new Phrase($"🏫 Класс: {className}", headerFont));
                        classTitleCell.BackgroundColor = new BaseColor(33, 150, 243);
                        classTitleCell.Padding = 8;
                        classTitleCell.PaddingBottom = 6;
                        classTitleCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        classTitleCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        classTitleCell.Border = Rectangle.BOX;
                        classTitleCell.BorderColor = new BaseColor(25, 118, 210);
                        classTitleCell.BorderWidth = 1.5f;
                        classTitleTable.AddCell(classTitleCell);
                        doc.Add(classTitleTable);
                        doc.Add(new Paragraph(" ", new Font(baseFont, 3, Font.NORMAL)));

                        PdfPTable table = new PdfPTable(7);
                        table.WidthPercentage = 100;
                        table.SetWidths(new float[] { 0.5f, 0.8f, 1.8f, 1.8f, 1.8f, 1.8f, 1.5f });
                        table.SpacingBefore = 2f;
                        table.SpacingAfter = 5f;
                        table.HorizontalAlignment = Element.ALIGN_CENTER;

                        // Заголовки дней недели
                        string[] dayHeaders = { "№", "Время", "Пн", "Вт", "Ср", "Чт", "Пт" };
                        BaseColor headerColor = new BaseColor(33, 150, 243);

                        for (int i = 0; i < dayHeaders.Length; i++)
                        {
                            PdfPCell cell = new PdfPCell(new Phrase(dayHeaders[i], headerFont));
                            cell.BackgroundColor = headerColor;
                            cell.HorizontalAlignment = Element.ALIGN_CENTER;
                            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                            cell.Padding = 5;
                            cell.PaddingTop = 4;
                            cell.PaddingBottom = 4;
                            cell.Border = Rectangle.BOX;
                            cell.BorderColor = new BaseColor(25, 118, 210);
                            cell.BorderWidth = 0.8f;
                            table.AddCell(cell);
                        }

                        int maxLessonNumber = lessons.Any() ? lessons.Max(l => l.LessonNumber) : 8;
                        if (maxLessonNumber > 9) maxLessonNumber = 9;

                        string[] days = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница" };

                        // Альтернативная подсветка строк
                        bool evenRow = false;

                        for (int lessonNum = 1; lessonNum <= maxLessonNumber; lessonNum++)
                        {
                            BaseColor rowBgColor = evenRow ? new BaseColor(240, 248, 255) : new BaseColor(255, 255, 255);
                            evenRow = !evenRow;

                            // Номер урока
                            PdfPCell numCell = new PdfPCell(new Phrase($"{lessonNum}", boldCellFont));
                            numCell.HorizontalAlignment = Element.ALIGN_CENTER;
                            numCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                            numCell.BackgroundColor = new BaseColor(220, 235, 250);
                            numCell.Padding = 3;
                            numCell.Border = Rectangle.BOX;
                            numCell.BorderWidth = 0.5f;
                            table.AddCell(numCell);

                            // Время
                            string time = GetLessonTimeWithEnd(lessonNum);
                            PdfPCell timeCell = new PdfPCell(new Phrase(time, smallFont));
                            timeCell.HorizontalAlignment = Element.ALIGN_CENTER;
                            timeCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                            timeCell.BackgroundColor = new BaseColor(220, 235, 250);
                            timeCell.Padding = 3;
                            timeCell.Border = Rectangle.BOX;
                            timeCell.BorderWidth = 0.5f;
                            table.AddCell(timeCell);

                            foreach (string day in days)
                            {
                                var lesson = lessons.FirstOrDefault(l =>
                                    l.DayOfWeek == day && l.LessonNumber == lessonNum);

                                PdfPCell cell = new PdfPCell();
                                cell.Padding = 4;
                                cell.PaddingTop = 3;
                                cell.PaddingBottom = 3;
                                cell.VerticalAlignment = Element.ALIGN_MIDDLE;
                                cell.HorizontalAlignment = Element.ALIGN_CENTER;
                                cell.Border = Rectangle.BOX;
                                cell.BorderWidth = 0.5f;
                                cell.BorderColor = new BaseColor(200, 200, 200);
                                cell.BackgroundColor = rowBgColor;

                                if (lesson != null)
                                {
                                    string subject = SafeString(lesson.Subject);
                                    string teacher = SafeString(lesson.Teacher);
                                    string room = SafeString(lesson.Room);

                                    // Умный перенос длинных названий предметов
                                    subject = WrapText(subject, 25);

                                    // Умный перенос длинных имен учителей
                                    teacher = WrapText(teacher, 25);

                                    // Форматирование номера кабинета
                                    string roomDisplay = FormatRoom(room);

                                    // Создаем содержимое ячейки
                                    Paragraph cellContent = new Paragraph();
                                    cellContent.Alignment = Element.ALIGN_CENTER;
                                    cellContent.Leading = 12f;

                                    // Предмет - жирным шрифтом
                                    Chunk subjectChunk = new Chunk(subject, subjectFont);
                                    cellContent.Add(subjectChunk);

                                    // Учитель - если не "Временный учитель ..."
                                    if (!string.IsNullOrEmpty(teacher) && teacher != "—" && !teacher.Contains("Временный"))
                                    {
                                        cellContent.Add(new Chunk("\n", teacherFont));
                                        Chunk teacherChunk = new Chunk(teacher, teacherFont);
                                        teacherChunk.Font.Color = new BaseColor(66, 66, 66);
                                        cellContent.Add(teacherChunk);
                                    }

                                    // Кабинет
                                    if (!string.IsNullOrEmpty(roomDisplay) && roomDisplay != "—" && roomDisplay != "")
                                    {
                                        cellContent.Add(new Chunk("\n", roomFont));
                                        Chunk roomChunk = new Chunk($"📍 {roomDisplay}", roomFont);
                                        roomChunk.Font.Color = BaseColor.GRAY;
                                        cellContent.Add(roomChunk);
                                    }

                                    cell.AddElement(cellContent);

                                    // Цветовая индикация статуса
                                    if (lesson.IsCanceled)
                                    {
                                        cell.BackgroundColor = new BaseColor(255, 210, 210);
                                        Paragraph strike = new Paragraph("❌ ОТМЕНЕН", new Font(baseFont, 6, Font.BOLD, BaseColor.RED));
                                        strike.Alignment = Element.ALIGN_CENTER;
                                        cell.AddElement(strike);
                                    }
                                    else if (!string.IsNullOrEmpty(lesson.LessonPlanTitle))
                                    {
                                        cell.BackgroundColor = new BaseColor(210, 240, 210);
                                    }
                                    else if (!string.IsNullOrEmpty(lesson.Note))
                                    {
                                        cell.BackgroundColor = new BaseColor(255, 245, 200);
                                    }
                                }
                                else
                                {
                                    // Пустая ячейка
                                    Paragraph emptyParagraph = new Paragraph("—", new Font(baseFont, 8, Font.NORMAL, BaseColor.LIGHT_GRAY));
                                    emptyParagraph.Alignment = Element.ALIGN_CENTER;
                                    cell.AddElement(emptyParagraph);
                                    cell.BackgroundColor = new BaseColor(248, 248, 248);
                                }

                                table.AddCell(cell);
                            }
                        }

                        doc.Add(table);
                    }

                    // Новая страница с легендой и статистикой
                    doc.NewPage();

                    // Легенда
                    PdfPTable legendTitleTable = new PdfPTable(1);
                    legendTitleTable.WidthPercentage = 60;
                    legendTitleTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    PdfPCell legendTitleCell = new PdfPCell(new Phrase("📋 Условные обозначения", headerFont));
                    legendTitleCell.BackgroundColor = new BaseColor(33, 150, 243);
                    legendTitleCell.Padding = 8;
                    legendTitleCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    legendTitleCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    legendTitleCell.Border = Rectangle.BOX;
                    legendTitleCell.BorderColor = new BaseColor(25, 118, 210);
                    legendTitleCell.BorderWidth = 1.5f;
                    legendTitleTable.AddCell(legendTitleCell);
                    doc.Add(legendTitleTable);
                    doc.Add(new Paragraph(" ", new Font(baseFont, 5, Font.NORMAL)));

                    PdfPTable legendTable = new PdfPTable(3);
                    legendTable.WidthPercentage = 80;
                    legendTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    legendTable.SetWidths(new float[] { 0.12f, 0.6f, 0.28f });
                    legendTable.SpacingBefore = 5f;
                    legendTable.SpacingAfter = 15f;

                    AddLegendItem(legendTable, new BaseColor(210, 240, 210), "📘 Есть план урока", "Привязан план урока");
                    AddLegendItem(legendTable, new BaseColor(255, 245, 200), "📝 Есть заметка", "Добавлена заметка");
                    AddLegendItem(legendTable, new BaseColor(255, 210, 210), "❌ Урок отменен", "Урок не состоится");

                    doc.Add(legendTable);

                    // Статистика
                    doc.Add(new Paragraph("📊 Статистика расписания", headerFont));
                    doc.Add(new Paragraph(" ", new Font(baseFont, 3, Font.NORMAL)));

                    PdfPTable statsTable = new PdfPTable(4);
                    statsTable.WidthPercentage = 80;
                    statsTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    statsTable.SetWidths(new float[] { 0.25f, 0.25f, 0.25f, 0.25f });
                    statsTable.SpacingBefore = 5f;

                    int teacherCount = schedule.Lessons
                        .Select(l => l.Teacher)
                        .Where(t => !string.IsNullOrEmpty(t) && !t.Contains("Временный") && t != "—")
                        .Distinct()
                        .Count();

                    AddStatsRow(statsTable, "📚 Всего уроков:", schedule.Lessons.Count.ToString());
                    AddStatsRow(statsTable, "🏫 Классов:", classGroups.Count.ToString());
                    AddStatsRow(statsTable, "👨‍🏫 Учителей:", teacherCount.ToString());
                    AddStatsRow(statsTable, "📖 Предметов:", schedule.Lessons.Select(l => l.Subject).Distinct().Count().ToString());

                    doc.Add(statsTable);

                    // Подвал
                    doc.Add(new Paragraph(" ", new Font(baseFont, 8, Font.NORMAL)));
                    PdfPTable footerTable = new PdfPTable(1);
                    footerTable.WidthPercentage = 100;
                    footerTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    PdfPCell footerCell = new PdfPCell(new Phrase(
                        $"Создано: {DateTime.Now:dd.MM.yyyy HH:mm}",
                        new Font(baseFont, 8, Font.NORMAL, BaseColor.GRAY)
                    ));
                    footerCell.Border = Rectangle.TOP_BORDER;
                    footerCell.BorderWidth = 1f;
                    footerCell.BorderColor = new BaseColor(200, 200, 200);
                    footerCell.Padding = 8;
                    footerCell.PaddingTop = 5;
                    footerCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    footerTable.AddCell(footerCell);
                    doc.Add(footerTable);

                    doc.Close();
                    MessageBox.Show("Расписание успешно экспортировано в PDF", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка экспорта PDF: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Умный перенос длинного текста с сохранением целых слов
        /// </summary>
        private string WrapText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            // Пробуем разбить по пробелам
            int splitIdx = text.LastIndexOf(' ', maxLength - 3);
            if (splitIdx > 0 && splitIdx < text.Length - 2)
            {
                return text.Substring(0, splitIdx) + "\n" + text.Substring(splitIdx + 1);
            }

            // Если не получилось разбить по пробелам - просто обрезаем
            if (text.Length > maxLength + 3)
            {
                return text.Substring(0, maxLength) + "…";
            }

            return text;
        }

        /// <summary>
        /// Форматирование отображения кабинета
        /// </summary>
        private string FormatRoom(string room)
        {
            if (string.IsNullOrEmpty(room) || room == "—")
                return "";

            string roomDisplay = room.Trim();

            if (roomDisplay.StartsWith("каб.") || roomDisplay.StartsWith("Каб."))
            {
                roomDisplay = roomDisplay.Replace("каб.", "").Replace("Каб.", "").Trim();
            }
            else if (roomDisplay.StartsWith("Спортзал") || roomDisplay.StartsWith("спортзал"))
            {
                roomDisplay = "🏟️ Спортзал";
            }
            else if (roomDisplay.StartsWith("Мастерская") || roomDisplay.StartsWith("мастерская"))
            {
                roomDisplay = "🔧 Мастерская";
            }

            return roomDisplay;
        }

        private void AddLegendItem(PdfPTable table, BaseColor color, string text, string desc)
        {
            PdfPCell colorCell = new PdfPCell();
            colorCell.BackgroundColor = color;
            colorCell.FixedHeight = 20;
            colorCell.Border = Rectangle.BOX;
            colorCell.BorderWidth = 0.5f;
            colorCell.BorderColor = new BaseColor(180, 180, 180);
            colorCell.HorizontalAlignment = Element.ALIGN_CENTER;
            colorCell.VerticalAlignment = Element.ALIGN_MIDDLE;
            table.AddCell(colorCell);

            PdfPCell textCell = new PdfPCell(new Phrase(text, new Font(baseFont, 9, Font.NORMAL)));
            textCell.Border = Rectangle.NO_BORDER;
            textCell.PaddingLeft = 10;
            textCell.VerticalAlignment = Element.ALIGN_MIDDLE;
            table.AddCell(textCell);

            PdfPCell descCell = new PdfPCell(new Phrase(desc, new Font(baseFont, 7, Font.NORMAL, BaseColor.GRAY)));
            descCell.Border = Rectangle.NO_BORDER;
            descCell.VerticalAlignment = Element.ALIGN_MIDDLE;
            descCell.HorizontalAlignment = Element.ALIGN_LEFT;
            table.AddCell(descCell);
        }

        private void AddStatsRow(PdfPTable table, string label, string value)
        {
            PdfPCell cell = new PdfPCell(new Phrase(label + " " + value, new Font(baseFont, 10, Font.NORMAL)));
            cell.Border = Rectangle.BOX;
            cell.BorderWidth = 0.5f;
            cell.BorderColor = new BaseColor(200, 200, 200);
            cell.Padding = 8;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.VerticalAlignment = Element.ALIGN_MIDDLE;
            cell.BackgroundColor = new BaseColor(245, 250, 255);
            table.AddCell(cell);
        }

        private string GetLessonTimeWithEnd(int lessonNumber)
        {
            switch (lessonNumber)
            {
                case 1: return "08:30\n09:10";
                case 2: return "09:20\n10:00";
                case 3: return "10:20\n11:00";
                case 4: return "11:10\n11:50";
                case 5: return "12:00\n12:40";
                case 6: return "13:00\n13:40";
                case 7: return "14:00\n14:40";
                case 8: return "14:50\n15:30";
                case 9: return "15:40\n16:20";
                default: return "";
            }
        }

        private string GetStatusText(LessonStatus status)
        {
            switch (status)
            {
                case LessonStatus.Draft: return "Черновик";
                case LessonStatus.Pending: return "На проверке";
                case LessonStatus.Approved: return "Утверждено";
                case LessonStatus.RequiresRevision: return "Требует доработки";
                default: return status.ToString();
            }
        }

        private string SafeString(string input)
        {
            return input ?? "—";
        }
    }
}