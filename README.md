# RaptorFlowGen

用文本脚本批量生成 **Raptor 流程图程序（`.rap` 文件）** 的小工具。

写一个几十行的文本脚本，一条命令就能得到可以在 Raptor 里打开、运行的流程图，适合批量出练习题、批量改题、做教学示例。

```
IN n
SET s := 0
SET i := 1
LOOP i > n
  SET s := s + i
  SET i := i + 1
ENDLOOP
OUT "sum = " + s
```

```
RaptorFlowGen.exe --raptor "C:\Raptor" --spec sum.txt --out sum.rap
```

---

## 下载

不想自己编译的话，直接到 **[Releases](https://github.com/holly00015/RaptorFlowGen/releases)** 下载 `RaptorFlowGen.exe`（Windows，需要 .NET Framework 4.x，且本机已安装 Raptor 4.0.5.x）。

下载后用法：

```bat
RaptorFlowGen.exe --raptor "<Raptor安装目录>" --spec examples\06_求1到n的和.txt --out out.rap
```

> 附件 SHA256：`3C42396870FB1A963C3BAC41925E37BE2703DDA89BE1665A984B7E639AF3C11E`

## 重要声明（请先读）

1. **本仓库不包含 Raptor。** Raptor 是 Martin C. Carlisle 开发的流程图教学软件，版权归其作者所有；中文汉化版由第三方翻译。本工具**只包含自己的代码**，运行时需要用户自行安装 Raptor，并把它指向安装目录。
2. **非官方工具。** 与 Raptor 作者、汉化者均无关联。
3. 本工具通过**反射调用 Raptor 自身的类和保存函数**来生成文件，不修改、不破解 Raptor 的任何可执行文件。
4. 请仅用于教学、学习等合法用途；用本工具生成的作品请遵守所在学校/机构的规定。

## 工作原理

Raptor 的 `.rap` 文件本质上是 .NET `BinaryFormatter` 序列化出来的对象图，根对象是流程图里的 `Start` 椭圆（`raptor.Oval`），通过 `Successor`、`yes_child/no_child`、`before_Child` 等字段连成一张图。

本工具做的事：

1. 加载用户目录下的 `raptor.exe`（.NET 程序集）以及它依赖的 DLL；
2. 用反射创建 Raptor **自己使用的**那些类：`raptor.Oval`、`raptor.Parallelogram`、`raptor.Rectangle`、`raptor.IF_Control`、`raptor.Loop`；
3. 按脚本语法树把这些对象连起来；
4. 调用 Raptor 自己的 `my_layout()` 排版、`Perform_Save()` 保存，得到合法的 `.rap` 文件。

换句话说：**复用它的运行时和序列化器，从外部喂对象给它**。

## 环境要求

| 项目 | 要求 |
|---|---|
| 操作系统 | Windows（需要 WinForms） |
| 运行时 | .NET Framework 4.x（Windows 10/11 自带） |
| Raptor | 已安装，且为 **4.0.5.x** 版本（`.rap` 为第 17 版二进制格式） |
| 编译 | `build.bat`（调用系统自带 `csc.exe`，无需 Visual Studio） |

> Raptor 6 及以后版本把 `.rap` 改成了 XML 格式，内部方法名也不同，本工具不能直接用，需要另做适配。

## 编译

```bat
build.bat
```

生成 `RaptorFlowGen.exe`。

## 使用

```bat
RaptorFlowGen.exe --raptor "<Raptor安装目录>" --spec "<脚本.txt>" --out "<输出.rap>"
```

参数：

| 参数 | 说明 |
|---|---|
| `--raptor <dir>` | Raptor 安装目录（含 `raptor.exe` 或 `Raptor 汉化版.exe`）。省略时依次读环境变量 `RAPTOR_DIR`、exe 同目录下的 `raptor.dir` 文件 |
| `--spec <file>` | 输入脚本（UTF-8） |
| `--out <file>` | 输出的 `.rap` 文件 |
| `--mode <m>` | `Intermediate`（默认）/ `Novice` / `Expert` |

也可以把目录写进 `raptor.dir`（exe 同目录，纯文本一行），以后就不用每次带 `--raptor`：

```
D:\Raptor 汉化版
```

生成的 `.rap` 双击即可用 Raptor 打开，按 F5 运行。

## 脚本语法

每行一条语句，`#` 开头是注释，缩进不影响语义（只是好看）。

| 语法 | 含义 |
|---|---|
| `IN 变量` | 输入符号，如 `IN n` |
| `OUT 表达式` | 输出符号，如 `OUT "sum = " + s` |
| `SET 变量 := 表达式` | 赋值符号，如 `SET s := s + i` |
| `IF 条件` … `ELSE` … `ENDIF` | 选择符号，`ELSE` 可省略 |
| `LOOP 条件` … `ENDLOOP` | 循环符号 |

表达式就是 Raptor 自己的语法：`+ - * /`、`mod`、`and/or/not`、`= <> < > <= >=`、字符串用双引号、`sqrt/floor/abs` 等内置函数。

### ⚠ 循环是“直到型”的

Raptor 的循环符号是 **repeat-until**：菱形里的条件**成立就退出**。所以想表达“当 i <= n 时反复执行”，条件要写成 `i > n`：

```
SET i := 1
LOOP i > n          # 循环直到 i > n
  ...
  SET i := i + 1
ENDLOOP
```

另外，变量名要避开 Raptor 的内置名（如 `pi`、`max` 等），否则表达式解析会失败。

## 示例

`examples/` 下有 15 个练习题的脚本（从两个数取较大值到九九乘法表、素数判断、最大公约数等），`examples/generated/` 里是对应的 15 个 `.rap`（均已在 Raptor 汉化版中打开并运行验证过）。可直接用来试：

```bat
for %f in (examples\*.txt) do RaptorFlowGen.exe --spec "%f" --out "examples\generated\%~nf.rap"
```

## 已知限制

- 只支持 输入、输出、赋值、选择、循环 五种符号；**不支持**数组、子图、函数/子程序和调用符号。
- 版面由 Raptor 自己的 `my_layout()` 计算，结构特别深时可能不够美观，可以打开后手工拖动。
- 库依赖 Raptor 的内部成员名（`Start`/`End`/`Perform_Save` 等），**换 Raptor 版本可能失效**，属于预期内的脆弱点。
- 生成的图中文以 .rap 内部的 UTF-16 字符串保存，正常显示；Raptor 控制台输出中文的编码取决于其自身实现。

### 已知的不稳定点（重要）

`Perform_Save()` 是 Raptor 内部的保存函数，它在某些环境组合下会“静默失败”（不报错、也不生成文件）：

- 输出目录与 Raptor 安装目录不在同一个盘符时更容易出现；
- 路径中含中文（如 `D:\考试\...`）时更容易出现；
- 连续快速调用多次时更容易出现。

**规避办法**：输出到纯 ASCII 路径、与 Raptor 同盘；批量生成时每个文件单独调用一次进程，失败就重试。如果要做成稳定产品，建议下一步不走 `Perform_Save`，而是自己用 `BinaryFormatter` 按 `.rap` 的字段顺序（版本号 → 反向循环标志 → 页数 → 页名/页类型 → Start 对象 → ink 字节 → log → 编译标志 → GUID）直接写文件，这样行为完全可控。本仓库目前保留了反射调用方案，作为可运行的参考实现。

## 排错

| 现象 | 原因 / 处理 |
|---|---|
| `raptor.exe not found` | `--raptor` 目录不对，或里面只有汉化版重命名的 exe（本工具会自动找名字含 raptor 的 exe） |
| `field not found / method not found` | Raptor 版本不对，本工具针对 4.0.5.x 编写 |
| 生成成功但打开报错 | 脚本里表达式 Raptor 解析不了（如用了内置名做变量），检查该行 |
| 打开后图形位置很乱 | 默认版面算法限制，手工拖一下即可，不影响运行 |

## 许可

本仓库代码使用 MIT 许可，详见 `LICENSE`。**该许可不覆盖 Raptor 本身**，Raptor 需另行合法获取。

## 致谢

- Raptor: Martin C. Carlisle, "RAPTOR: a visual programming environment for teaching algorithmic problem solving", SIGCSE 2005.
