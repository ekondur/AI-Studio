# AI Studio

[![Codacy Badge](https://app.codacy.com/project/badge/Grade/400ac43e51b04f3fb2f335c1688b8d4b)](https://app.codacy.com/gh/ekondur/AI-Studio/dashboard?utm_source=gh&utm_medium=referral&utm_content=&utm_campaign=Badge_grade)

> Supercharge your Visual Studio workflow with AI-powered tools like code generation, refactoring, commenting, unit test creation, and more, right from the right-click menu.

🎯 [Available on Visual Studio Marketplace →](https://marketplace.visualstudio.com/items?itemName=ekondur.AI-Studio)

![AI Studio Preview](https://user-images.githubusercontent.com/4971326/234110009-382af5bf-9bc8-4bec-892b-90bf66b03fa3.png)


## 🚀 Getting Started

1. **Get Your API Key**  
   Sign in to [OpenAI](https://platform.openai.com/account/api-keys) to create your API key.

2. **Configure AI Studio**  
   Go to `Tools → Options → AI Studio` and paste in your API key.

   ![Configuration Screenshot](https://github.com/user-attachments/assets/3d50b0f4-b127-48ed-892d-94db90d4ca02)


## ⚙️ Configuration Options

- **Format Changed Text**: Automatically formats AI-generated/refactored code.  
- **Language Model**: Switch between models or use a custom one for your endpoint.


## ✨ Features

### 🔧 Code It

Generate code for stubs or comments:

1. Select code or place cursor on a line  
2. Right-click → `AI Studio → Code It`  
3. View AI-generated implementation

![Code It](https://user-images.githubusercontent.com/4971326/232882864-85547d6f-75ee-4d49-8684-a3b736b5da2e.png)

**Result**:  
![Code Output](https://user-images.githubusercontent.com/4971326/232883443-de21b5c2-3415-4f5b-bed9-49077bf7732c.png)


### 💬 Add Comments

1. Select code  
2. Right-click → `AI Studio → Add Comments`  
3. Receive clean, inline comments

![Add Comments](https://user-images.githubusercontent.com/4971326/232887104-8778b163-6cbf-4dcb-a12b-caa6ba266565.png)


### 🔁 Refactor

1. Select a method  
2. Right-click → `AI Studio → Refactor`  
3. View optimized code

![Refactor](https://user-images.githubusercontent.com/4971326/232884573-c8f18fc5-3564-4d8d-ad3a-742b85142b36.png)


### 📝 Add Summary

1. Select a method or its header  
2. Right-click → `AI Studio → Add Summary`  
3. Generate XML-style documentation

![Summary](https://user-images.githubusercontent.com/4971326/232885737-84f7befa-1cad-4ff7-ba10-4b84f659b2fc.png)


### 📖 Explain

1. Select code  
2. Right-click → `AI Studio → Explain`  
3. See code explanation in a popup

![Explain](https://github.com/user-attachments/assets/3c419429-2586-428d-a1ef-599803d137da)


### 🔐 Security Check

1. Select code  
2. Right-click → `AI Studio → Security Check`  
3. View potential vulnerabilities and suggestions

![Security Check](https://github.com/user-attachments/assets/37dbecc7-9894-49ed-a70c-efe3bb8d03a8)


## 🧪 Unit Test Generation

1. Select a method  
2. Right-click → `AI Studio → Add Unit Tests`  
3. Instantly generate unit tests

![Unit Test Example](https://github.com/user-attachments/assets/728816aa-228d-4b06-adbb-bd79e75ae633)

### Test Configuration

Configure test behaviors via:  
`Tools → Options → AI Studio → Unit Test`

![Unit Test Settings](https://user-images.githubusercontent.com/4971326/232892595-9e304843-8b0d-4420-b058-a0f44688f46e.png)

#### Supported Frameworks

- **Unit Testing**
  - [MSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-mstest)
  - [xUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test)
  - [NUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-nunit)

- **Mocking & Isolation**
  - [Moq](https://github.com/Moq/moq4)
  - [FakeItEasy](https://github.com/FakeItEasy/FakeItEasy)
  - [NSubstitute](https://github.com/nsubstitute/NSubstitute)

- **Test Data Generators**
  - [AutoFixture](https://github.com/AutoFixture/AutoFixture)
  - [Bogus](https://github.com/bchavez/Bogus)
  - [GenFu](https://github.com/MisterJames/GenFu)
  - [NBuilder](https://github.com/nbuilder/nbuilder)
  - [AutoBogus](https://github.com/nickdodd79/AutoBogus)

- **Assertion Libraries**
  - [FluentAssertions](https://fluentassertions.com/introduction)
  - [Shouldly](https://docs.shouldly.org/)
  - [NFluent](https://github.com/tpierrain/NFluent)

- **Custom Instructions**  
  Add special rules or constraints for your unit test generation.


## 🧩 Advanced Customization

### ✏️ Command Customization

Modify AI Studio behaviors using your own instructions:

1. Go to `Tools → Options → AI Studio → Commands`  
2. Customize each action with your own prompt/instructions

![Command Settings](https://github.com/ekondur/AI-Studio/assets/4971326/0b49f17d-fa00-40dd-a1d3-ff8aa7e43f2d)

**Example Result**:  
![Custom Command](https://user-images.githubusercontent.com/4971326/232890352-64908383-623b-43f7-8dfa-32f305f67a43.png)


## 📌 Summary

AI Studio makes AI assistance a seamless part of your development workflow in Visual Studio. Whether you're generating code, documenting, testing, or refactoring—it’s all just a right-click away.
