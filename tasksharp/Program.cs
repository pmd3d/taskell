// todo: null parser values, rewrite them out?
// todo: implement config class
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Data Structures
public class Due
{
    public DateTime DueDate { get; set; }
}

public class Subtask
{
    public bool Complete { get; set; }
    public string Name { get; set; }

    public Subtask(bool complete, string name)
    {
        Complete = complete;
        Name = name;
    }
}

public class Task
{
    public string Name { get; set; }
    public Maybe<Due> DueDate { get; set; }
    public Maybe<string> Description { get; set; }
    public List<Subtask> Subtasks { get; set; }

    public Task(string name)
    {
        Name = name;
        Subtasks = new List<Subtask>();
        DueDate = Maybe<Due>.Nothing();
        Description = Maybe<string>.Nothing();
    }
}

public class List
{
    public string Title { get; set; }
    public List<Task> Tasks { get; set; }

    public List(string title)
    {
        Title = title;
        Tasks = new List<Task>();
    }
}

public class Lists
{
    public List<List> Items { get; }

    public Lists(List<List> items)
    {
        Items = items;
    }
}

// Maybe<T> Type
public abstract class Maybe<T>
{
    public static Maybe<T> Just(T value) => new Just<T>(value);
    public static Maybe<T> Nothing() => new Nothing<T>();
}

public sealed class Just<T> : Maybe<T>
{
    public T Value { get; }
    public Just(T value) { Value = value; }
}

public sealed class Nothing<T> : Maybe<T>
{
}

// Configuration Class
public class Config
{
    public required string DescriptionOutput { get; set; }
    public required string DueOutput { get; set; }
    public required string SubtaskOutput { get; set; }
    public required string TaskOutput { get; set; }
    public required string TitleOutput { get; set; }
    public required bool LocalTimes { get; set; }
}

// Parser Combinators
public struct Unit
{
    public static readonly Unit Instance = new Unit();
}

public class ParseResult<T>
{
    public T Value { get; }
    public string Remaining { get; }

    public ParseResult(T value, string remaining)
    {
        Value = value;
        Remaining = remaining;
    }
}

public delegate Maybe<ParseResult<T>> Parser<T>(string input);

public static class ParserCombinators
{
    // Basic Parsers
    public static Parser<char> Satisfy(Func<char, bool> predicate)
    {
        return input =>
        {
            if (string.IsNullOrEmpty(input))
                return Maybe<ParseResult<char>>.Nothing();

            char firstChar = input[0];
            if (predicate(firstChar))
                return Maybe<ParseResult<char>>.Just(new ParseResult<char>(firstChar, input.Substring(1)));
            else
                return Maybe<ParseResult<char>>.Nothing();
        };
    }

    public static Parser<char> Char(char c)
    {
        return Satisfy(ch => ch == c);
    }

    public static Parser<string> String(string s)
    {
        return input =>
        {
            if (input.StartsWith(s))
                return Maybe<ParseResult<string>>.Just(new ParseResult<string>(s, input.Substring(s.Length)));
            else
                return Maybe<ParseResult<string>>.Nothing();
        };
    }

    public static Parser<string> Line()
    {
        return input =>
        {
            int index = input.IndexOfAny(new[] { '\r', '\n' });
            if (index == -1)
                return Maybe<ParseResult<string>>.Just(new ParseResult<string>(input, string.Empty));
            else
            {
                string line = input.Substring(0, index);
                string remaining = input.Substring(index).TrimStart('\r', '\n');
                return Maybe<ParseResult<string>>.Just(new ParseResult<string>(line, remaining));
            }
        };
    }

    // Combinators
    public static Parser<R> Bind<T, R>(this Parser<T> parser, Func<T, Parser<R>> func)
    {
        return input =>
        {
            var result = parser(input);
            if (result is Just<ParseResult<T>> justResult)
            {
                return func(justResult.Value.Value)(justResult.Value.Remaining);
            }
            else
            {
                return Maybe<ParseResult<R>>.Nothing();
            }
        };
    }

    public static Parser<T> Or<T>(this Parser<T> parser1, Parser<T> parser2)
    {
        return input =>
        {
            var result = parser1(input);
            if (result is Just<ParseResult<T>>)
                return result;
            else
                return parser2(input);
        };
    }

    public static Parser<List<T>> Many<T>(Parser<T> parser)
    {
        return input =>
        {
            var results = new List<T>();
            var remainder = input;

            while (true)
            {
                var result = parser(remainder);
                if (result is Just<ParseResult<T>> justResult)
                {
                    results.Add(justResult.Value.Value);
                    remainder = justResult.Value.Remaining;
                }
                else
                {
                    break;
                }
            }

            return Maybe<ParseResult<List<T>>>.Just(new ParseResult<List<T>>(results, remainder));
        };
    }

    public static Parser<List<T>> Many1<T>(Parser<T> parser)
    {
        return parser.Bind(first =>
            Many(parser).Bind(rest =>
                Return(new List<T> { first }.Concat(rest).ToList())
            )
        );
    }

    public static Parser<Maybe<T>> Optional<T>(Parser<T> parser)
    {
        return input =>
        {
            var result = parser(input);
            if (result is Just<ParseResult<T>> justResult)
            {
                return Maybe<ParseResult<Maybe<T>>>.Just(new ParseResult<Maybe<T>>(Maybe<T>.Just(justResult.Value.Value), justResult.Value.Remaining));
            }
            else
            {
                return Maybe<ParseResult<Maybe<T>>>.Just(new ParseResult<Maybe<T>>(Maybe<T>.Nothing(), input));
            }
        };
    }

    public static Parser<T> Return<T>(T value)
    {
        return input => Maybe<ParseResult<T>>.Just(new ParseResult<T>(value, input));
    }

    public static Parser<R> Select<T, R>(this Parser<T> parser, Func<T, R> selector)
    {
        return input =>
        {
            var result = parser(input);
            if (result is Just<ParseResult<T>> justResult)
            {
                return Maybe<ParseResult<R>>.Just(new ParseResult<R>(selector(justResult.Value.Value), justResult.Value.Remaining));
            }
            else
            {
                return Maybe<ParseResult<R>>.Nothing();
            }
        };
    }

    public static Parser<R> SelectMany<T, U, R>(this Parser<T> parser, Func<T, Parser<U>> func, Func<T, U, R> selector)
    {
        return parser.Bind(t => func(t).Select(u => selector(t, u)));
    }

    // Helper Parsers
    public static Parser<Unit> EndOfInput()
    {
        return input =>
        {
            if (string.IsNullOrEmpty(input))
                return Maybe<ParseResult<Unit>>.Just(new ParseResult<Unit>(Unit.Instance, input));
            else
                return Maybe<ParseResult<Unit>>.Nothing();
        };
    }

    public static Parser<string> SkipSpaces()
    {
        return Many(Satisfy(char.IsWhiteSpace)).Select(chars => new string(chars.ToArray()));
    }
}

// Parser Implementation
public static class MarkdownParser
{
    public delegate Parser<Unit> Symbol(Func<Config, string> fn);

    public static Symbol SymP(Config config)
    {
        return fn => ParserCombinators.String(fn(config)).Bind(_ => ParserCombinators.Char(' ').Select(__ => Unit.Instance));
    }

    public static Parser<bool> SubtaskCompleteP()
    {
        return from lbracket in ParserCombinators.Char('[')
               from x in ParserCombinators.Char('x').Or(ParserCombinators.Char(' '))
               from rbracket in ParserCombinators.Char(']')
               from space in ParserCombinators.Char(' ')
               select x == 'x';
    }

    public static Parser<Subtask> SubtaskP(Symbol sym, Config config)
    {
        return sym(c => c.SubtaskOutput).Bind(_ =>
            SubtaskCompleteP().Bind(complete =>
                ParserCombinators.Line().Select(name => new Subtask(complete, name))
            )
        );
    }

    public static Parser<Maybe<string>> TaskDescriptionP(Symbol sym, Config config)
    {
        return ParserCombinators.Many(sym(c => c.DescriptionOutput).Bind(_ => ParserCombinators.Line()))
            .Select(lines =>
            {
                var description = string.Join("\n", lines);
                return string.IsNullOrWhiteSpace(description) ? Maybe<string>.Nothing() : Maybe<string>.Just(description);
            });
    }

    public static Parser<Maybe<Due>> DueP(Symbol sym, Config config)
    {
        return ParserCombinators.Optional(sym(c => c.DueOutput).Bind(_ => ParserCombinators.Line()))
            .Select(dueTextMaybe =>
            {
                if (dueTextMaybe is Just<string> justDueText)
                {
                    var dueText = justDueText.Value;
                    if (DateTime.TryParse(dueText, out var date))
                        return Maybe<Due>.Just(new Due { DueDate = date });
                    else
                        throw new Exception("Invalid due date format: " + dueText);
                }
                else
                {
                    return Maybe<Due>.Nothing();
                }
            });
    }

    public static Parser<string> TaskNameP(Symbol sym, Config config)
    {
        return sym(c => c.TaskOutput).Bind(_ => ParserCombinators.Line());
    }

    public static Parser<Task> TaskP(Symbol sym, Config config)
    {
        return from name in TaskNameP(sym, config)
               from due in DueP(sym, config)
               from description in TaskDescriptionP(sym, config)
               from subtasks in ParserCombinators.Many(SubtaskP(sym, config))
               select new Task(name)
               {
                   DueDate = due,
                   Description = description,
                   Subtasks = subtasks
               };
    }

    public static Parser<string> ListTitleP(Symbol sym, Config config)
    {
        return sym(c => c.TitleOutput).Bind(_ => ParserCombinators.Line());
    }

    public static Parser<List> ListP(Symbol sym, Config config)
    {
        return from title in ListTitleP(sym, config)
               from tasks in ParserCombinators.Many(TaskP(sym, config))
               select new List(title)
               {
                   Tasks = tasks
               };
    }

    public static Parser<Lists> MarkdownP(Symbol sym, Config config)
    {
        return ParserCombinators.Many1(ListP(sym, config))
            .Bind(lists =>
                ParserCombinators.SkipSpaces().Bind(_ =>
                    ParserCombinators.EndOfInput().Select(__ => new Lists(lists))
                )
            );
    }

    public static Lists Parse(Config config, string text)
    {
        var sym = SymP(config);
        var parser = MarkdownP(sym, config);

        var result = parser(text);
        if (result is Just<ParseResult<Lists>> justResult)
            return justResult.Value.Value;
        else
            throw new Exception("Could not parse file.");
    }
}


// MarkdownInfo Class
public class MarkdownInfo
{
    public TimeZoneInfo TimeZone { get; set; }
    public Config Config { get; set; }

    public MarkdownInfo(TimeZoneInfo timeZone, Config config)
    {
        TimeZone = timeZone;
        Config = config;
    }
}

// Serializer Implementation
public static class MarkdownSerializer
{
    // Utility Functions

    // Concatenates symbol and text with a space
    private static string Space(string symbol, string text)
    {
        return $"{symbol} {text}";
    }

    // Converts a bool to "[x]" or "[ ]"
    private static string SubtaskCompleteS(bool complete)
    {
        return complete ? "[x]" : "[ ]";
    }

    // Handles optional values (Maybe<T>)
    private static string StrMay<T>(Func<T, string> fn, Maybe<T> maybe)
    {
        if (maybe is Just<T> just)
        {
            return fn(just.Value);
        }
        else
        {
            return "";
        }
    }

    // Time formatting functions
    private static string TimeToOutput(Due due)
    {
        // Formats the due date in UTC
        return due.DueDate.ToUniversalTime().ToString("o"); // ISO 8601 format
    }

    private static string TimeToOutputLocal(Due due, TimeZoneInfo timeZone)
    {
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(due.DueDate.ToUniversalTime(), timeZone);
        return localTime.ToString("o");
    }

    private static Func<Due, string> TimeFn(MarkdownInfo mdInfo)
    {
        if (mdInfo.Config.LocalTimes)
        {
            return due => TimeToOutputLocal(due, mdInfo.TimeZone);
        }
        else
        {
            return due => TimeToOutput(due);
        }
    }

    // Serializers

    // Serializes a subtask
    private static string SubtaskS(Subtask subtask, Config config)
    {
        string symbol = config.SubtaskOutput;
        return $"{symbol} {SubtaskCompleteS(subtask.Complete)} {subtask.Name}";
    }

    // Serializes a list of subtasks
    private static string SubtasksS(List<Subtask> subtasks, Config config)
    {
        var lines = subtasks.Select(subtask => SubtaskS(subtask, config));
        return string.Join("\n", lines);
    }

    // Serializes the description
    private static string DescriptionS(string description, Config config)
    {
        string symbol = config.DescriptionOutput;
        var lines = description.Split(new[] { '\r', '\n' }, StringSplitOptions.None)
                                .Select(line => Space(symbol, line));
        return string.Join("\n", lines);
    }

    // Serializes the due date
    private static string DueS(Due due, MarkdownInfo mdInfo)
    {
        string symbol = mdInfo.Config.DueOutput;
        Func<Due, string> fn = TimeFn(mdInfo);
        return Space(symbol, fn(due));
    }

    // Serializes the task name
    private static string NameS(string name, Config config)
    {
        return Space(config.TaskOutput, name);
    }

    // Serializes a task
    private static string TaskS(Task task, MarkdownInfo mdInfo)
    {
        var lines = new List<string>();

        // Task name
        lines.Add(NameS(task.Name, mdInfo.Config));

        // Due date
        string dueLine = StrMay(due => DueS(due, mdInfo), task.DueDate);
        if (!string.IsNullOrEmpty(dueLine))
        {
            lines.Add(dueLine);
        }

        // Description
        string descriptionLine = StrMay(desc => DescriptionS(desc, mdInfo.Config), task.Description);
        if (!string.IsNullOrEmpty(descriptionLine))
        {
            lines.Add(descriptionLine);
        }

        // Subtasks
        if (task.Subtasks.Count > 0)
        {
            string subtasksLine = SubtasksS(task.Subtasks, mdInfo.Config);
            if (!string.IsNullOrEmpty(subtasksLine))
            {
                lines.Add(subtasksLine);
            }
        }

        return string.Join("\n", lines.Where(line => !string.IsNullOrEmpty(line)));
    }

    // Serializes a list
    private static string ListS(List list, MarkdownInfo mdInfo)
    {
        string symbol = mdInfo.Config.TitleOutput;
        var taskStrings = list.Tasks.Select(task => TaskS(task, mdInfo));

        string tasksString = string.Join("\n", taskStrings);

        if (!string.IsNullOrWhiteSpace(tasksString))
        {
            tasksString += "\n";
        }

        return Space(symbol, list.Title + "\n\n" + tasksString);
    }

    // Serializes all lists
    public static string Serialize(Lists lists, MarkdownInfo mdInfo)
    {
        var listStrings = lists.Items.Select(list => ListS(list, mdInfo));
        return string.Join("\n", listStrings);
    }
}

class TaskSharp
{
    public static void Main()
    {
        Config config = new Config
        {
            TitleOutput = "##",
            TaskOutput = "-",
            DescriptionOutput = "    >",
            DueOutput = "    @",
            SubtaskOutput = "    *",
            LocalTimes = false
        };

        string input = File.ReadAllText("taskell.md");
        Lists lists = MarkdownParser.Parse(config, input);

        // Time Zone
        var timeZone = TimeZoneInfo.Local; // Use local time zone or specify as needed
        // MarkdownInfo
        var mdInfo = new MarkdownInfo(timeZone, config);

        string output = MarkdownSerializer.Serialize(lists, mdInfo);
        File.WriteAllText("tasksharp.md", output);
    }
}
