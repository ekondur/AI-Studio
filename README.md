# AI Studio
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/400ac43e51b04f3fb2f335c1688b8d4b)](https://app.codacy.com/gh/ekondur/AI-Studio/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)
> AI Studio helps you with the power of chatGPT in many subjects such as adding unit tests, refactoring code, adding summary, etc. while writing code, just by right clicking on the code.

[Get it from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=ekondur.AI-Studio)

![image](https://user-images.githubusercontent.com/4971326/234110009-382af5bf-9bc8-4bec-892b-90bf66b03fa3.png)

- First, you need to create an API key from the https://platform.openai.com/account/api-keys
- Go to **Tools/Options/AI Studio** page and write the key in the General section

![image](https://github.com/ekondur/AI-Studio/assets/4971326/896600b6-14c5-4e1a-aaf6-087b4ceb141c)

- If you prefer refactoring the code changes, keep **Format Changed Text** as True.
- Choose a **Language Model** options: "ChatGPTTurbo", GPT4_Turbo, "GPT4" or "GPT4_32k_Context"

### Code It:
Write a use case where you want to write the code, select the statement (if it's a single line just click on the line), right-click, and click "AI Studio / **Code It**".

![image](https://user-images.githubusercontent.com/4971326/232882864-85547d6f-75ee-4d49-8684-a3b736b5da2e.png)

Prints the result after a short time:

![image](https://user-images.githubusercontent.com/4971326/232883443-de21b5c2-3415-4f5b-bed9-49077bf7732c.png)

### Add Comments:
Select the lines of code you want to comment on, right-click, and click "AI Studio / **Add Comments**".

Returns the selected code with detailed comments.

![image](https://user-images.githubusercontent.com/4971326/232887104-8778b163-6cbf-4dcb-a12b-caa6ba266565.png)

### Refactor:
Select the whole method, right-click, and click "AI Studio / **Refactor**".

The refactored result:

![image](https://user-images.githubusercontent.com/4971326/232884573-c8f18fc5-3564-4d8d-ad3a-742b85142b36.png)

### Add Summary:
Select the whole method or just the first line of the method, right-click, and click "AI Studio / **Add Summary**".

Gives a very detailed and logical result:

![image](https://user-images.githubusercontent.com/4971326/232885737-84f7befa-1cad-4ff7-ba10-4b84f659b2fc.png)

### Explain:
Select the lines of code you want to explain, right-click, and click "AI Studio / **Explain**".

Shows a popup that includes explanations of the selected code:

![image](https://user-images.githubusercontent.com/4971326/232888457-c12651dd-abcf-48f1-a0a5-578aaacfff06.png)

#### Customizable Commands
- AI studio is a flexible tool that allows you to customize all commands. Go to *Tools/Options/AI Studio/Commands*,
- Write something to help chatGPT about the behaviors of the commands.

![image](https://github.com/ekondur/AI-Studio/assets/4971326/0b49f17d-fa00-40dd-a1d3-ff8aa7e43f2d)

Then trigger the command again, and you will see the results affected by your comments:

![image](https://user-images.githubusercontent.com/4971326/232890352-64908383-623b-43f7-8dfa-32f305f67a43.png)

### Add Unit Tests:
Select the whole method, right-click, and click "AI Studio / **Add Unit Tests**".

Prints the unit test(s) based on your choices:

![image](https://user-images.githubusercontent.com/4971326/232892126-91f3c335-3633-4b4f-8c27-2da5b404e329.png)

You can also customize the unit tests on *Tools/Options/AI Studio/Unit Test*

![image](https://user-images.githubusercontent.com/4971326/232892595-9e304843-8b0d-4420-b058-a0f44688f46e.png)

- **Unit Testing Framework:** Select unit testing framework to set up main functionalities.
  - [MSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-mstest)
  - [xUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)
  - [NUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-nunit)
- **Isolation Framework:** An isolation framework is a set of programmable APIs that makes creating fake objects much simpler, faster, and shorter than hand-coding them.
  - [Moq](https://github.com/Moq/moq4)
  - [FakeItEasy](https://github.com/FakeItEasy/FakeItEasy)
  - [NSubstitute](https://github.com/nsubstitute/NSubstitute)
- **Test/Dummy Data Framework:** Test Data Builders and Dummy Data Generators.
  - [AutoFixture](https://github.com/AutoFixture/AutoFixture)
  - [Bogus](https://github.com/bchavez/Bogus)
  - [GenFu](https://github.com/MisterJames/GenFu)
  - [NBuilder](https://github.com/nbuilder/nbuilder)
  - [AutoBogus](https://github.com/nickdodd79/AutoBogus)
- **Fluent Assertions Framework:** Fluent assertions frameworks is a set of .NET extension methods that allow you to more naturally specify the expected outcome of a TDD or BDD-style unit test.
  - [FluentAssertions](https://fluentassertions.com/introduction)
  - [Shouldly](https://docs.shouldly.org/)
  - [NFluent](https://github.com/tpierrain/NFluent)
- **Customize:** Add any other details to customize unit tests.

### Security Check:
Select the code line(s), right-click, and click "AI Studio / **Security Check**".

Gives some information and suggestions:

![image](https://user-images.githubusercontent.com/4971326/234108978-486678ec-b2c3-4258-8a3d-e1b9488b9fb3.png)
