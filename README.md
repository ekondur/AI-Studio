# AI Studio

[![Codacy Badge](https://app.codacy.com/project/badge/Grade/400ac43e51b04f3fb2f335c1688b8d4b)](https://app.codacy.com/gh/ekondur/AI-Studio/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)

> AI Studio enhances your coding workflow with ChatGPT-powered features like unit test generation, code refactoring, documentation, and more - accessible via right-click context menu.

[Get it from Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=ekondur.AI-Studio)

![Extension Preview](https://user-images.githubusercontent.com/4971326/234110009-382af5bf-9bc8-4bec-892b-90bf66b03fa3.png)

## Getting Started

1. **Obtain API Key**: Create your API key at [OpenAI Platform](https://platform.openai.com/account/api-keys)
2. **Configure Extension**: Navigate to **Tools/Options/AI Studio** and enter your key
   
   ![Settings Page](https://github.com/user-attachments/assets/36623c4d-c99f-4f6c-a3c8-51a9445bdc11)

### Configuration Options
- **Format Changed Text**: Enable to automatically format refactored code
- **Language Model**: Choose between:
  - ChatGPTTurbo
  - GPT4_Turbo
  - GPT4
  - GPT4o
  - GPT4_32k_Context
- **Custom Models**: Optionally specify custom models

  ![Custom Model Settings](https://github.com/user-attachments/assets/c1b8c35a-9719-4ec5-9146-112d347fc522)

## Features

### Code It
1. Select code or click on a line
2. Right-click → "AI Studio / **Code It**"
3. Get AI-generated implementation

![Code It Example](https://user-images.githubusercontent.com/4971326/232882864-85547d6f-75ee-4d49-8684-a3b736b5da2e.png)

**Result**:
![Code It Output](https://user-images.githubusercontent.com/4971326/232883443-de21b5c2-3415-4f5b-bed9-49077bf7732c.png)

### Add Comments
1. Select code
2. Right-click → "AI Studio / **Add Comments**"
3. Receive commented code

![Add Comments Example](https://user-images.githubusercontent.com/4971326/232887104-8778b163-6cbf-4dcb-a12b-caa6ba266565.png)

### Refactor
1. Select method
2. Right-click → "AI Studio / **Refactor**"
3. Get optimized code

![Refactor Example](https://user-images.githubusercontent.com/4971326/232884573-c8f18fc5-3564-4d8d-ad3a-742b85142b36.png)

### Add Summary
1. Select method or method header
2. Right-click → "AI Studio / **Add Summary**"
3. Receive detailed documentation

![Summary Example](https://user-images.githubusercontent.com/4971326/232885737-84f7befa-1cad-4ff7-ba10-4b84f659b2fc.png)

### Explain
1. Select code
2. Right-click → "AI Studio / **Explain**"
3. View code explanation popup

![Explain Example](https://github.com/user-attachments/assets/3c419429-2586-428d-a1ef-599803d137da)

### Security Check
1. Select code
2. Right-click → "AI Studio / **Security Check**"
3. Review security suggestions

![Security Check Example](https://github.com/user-attachments/assets/37dbecc7-9894-49ed-a70c-efe3bb8d03a8)

## Advanced Customization

### Command Customization
1. Navigate to *Tools/Options/AI Studio/Commands*
2. Modify command behaviors with custom instructions

![Command Settings](https://github.com/ekondur/AI-Studio/assets/4971326/0b49f17d-fa00-40dd-a1d3-ff8aa7e43f2d)

**Customization Result**:
![Custom Command Example](https://user-images.githubusercontent.com/4971326/232890352-64908383-623b-43f7-8dfa-32f305f67a43.png)

### Unit Test Generation
1. Select method
2. Right-click → "AI Studio / **Add Unit Tests**"
3. Get generated tests

![Unit Test Example](https://github.com/user-attachments/assets/728816aa-228d-4b06-adbb-bd79e75ae633)

Configure test generation at *Tools/Options/AI Studio/Unit Test*:

![Unit Test Settings](https://user-images.githubusercontent.com/4971326/232892595-9e304843-8b0d-4420-b058-a0f44688f46e.png)

#### Testing Framework Options
- **Unit Testing**:
  - [MSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-mstest)
  - [xUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)
  - [NUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-nunit)
  
- **Isolation Frameworks**:
  - [Moq](https://github.com/Moq/moq4)
  - [FakeItEasy](https://github.com/FakeItEasy/FakeItEasy)
  - [NSubstitute](https://github.com/nsubstitute/NSubstitute)
  
- **Test Data**:
  - [AutoFixture](https://github.com/AutoFixture/AutoFixture)
  - [Bogus](https://github.com/bchavez/Bogus)
  - [GenFu](https://github.com/MisterJames/GenFu)
  - [NBuilder](https://github.com/nbuilder/nbuilder)
  - [AutoBogus](https://github.com/nickdodd79/AutoBogus)
  
- **Assertion Libraries**:
  - [FluentAssertions](https://fluentassertions.com/introduction)
  - [Shouldly](https://docs.shouldly.org/)
  - [NFluent](https://github.com/tpierrain/NFluent)
  
- **Custom Instructions**: Add specific requirements for test generation
