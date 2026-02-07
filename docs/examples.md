# Examples and Use Cases

## Table of Contents

- [Basic Examples](#basic-examples)
- [Data Processing](#data-processing)
- [File Operations](#file-operations)
- [Web Scraping](#web-scraping)
- [Advanced Use Cases](#advanced-use-cases)

## Basic Examples

### Example 1: Hello World

**Task**: Create a simple Python script that prints "Hello World".

```bash
dotnet run --project RR.Agent -- "Create a Python script that prints 'Hello World'"
```

**Expected Workflow**:
1. Planner creates a simple plan with one step
2. Executor writes a Python script
3. Executor executes the script
4. Evaluator confirms success
5. Task completes

**Generated Script** (`workspace/scripts/hello.py`):
```python
print("Hello World")
```

**Output**:
```
Hello World
```

### Example 2: Fibonacci Sequence

**Task**: Calculate and display Fibonacci numbers.

```bash
dotnet run --project RR.Agent -- "Create a Python script that calculates the first 10 Fibonacci numbers"
```

**Expected Plan**:
1. Write Python script to calculate Fibonacci sequence
2. Execute script and display results

**Generated Script** (`workspace/scripts/fibonacci.py`):
```python
def fibonacci(n):
    fib_sequence = [0, 1]
    for i in range(2, n):
        fib_sequence.append(fib_sequence[i-1] + fib_sequence[i-2])
    return fib_sequence

# Calculate first 10 Fibonacci numbers
result = fibonacci(10)
print("First 10 Fibonacci numbers:")
print(result)
```

**Output**:
```
First 10 Fibonacci numbers:
[0, 1, 1, 2, 3, 5, 8, 13, 21, 34]
```

### Example 3: Simple Calculation

**Task**: Perform mathematical calculations.

```bash
dotnet run --project RR.Agent -- "Calculate the area of a circle with radius 5"
```

**Generated Script**:
```python
import math

radius = 5
area = math.pi * radius ** 2
print(f"Area of circle with radius {radius}: {area:.2f}")
```

**Output**:
```
Area of circle with radius 5: 78.54
```

## Data Processing

### Example 4: CSV Data Analysis

**Task**: Read and analyze CSV data.

**Prerequisites**: Have a CSV file at a known location (e.g., `C:\Users\YourName\data.csv`)

```bash
dotnet run --project RR.Agent -- "Read the CSV file at C:\Users\YourName\data.csv and calculate the average of the 'price' column"
```

**Expected Plan**:
1. Copy CSV file to workspace
2. Install pandas package
3. Read CSV and calculate average
4. Display results

**Generated Script**:
```python
import pandas as pd

# Read CSV file
df = pd.read_csv('data.csv')

# Calculate average of price column
avg_price = df['price'].mean()

print(f"Average price: ${avg_price:.2f}")
print(f"Total records: {len(df)}")
```

**Sample Output**:
```
Average price: $45.67
Total records: 150
```

### Example 5: JSON Processing

**Task**: Create and manipulate JSON data.

```bash
dotnet run --project RR.Agent -- "Create a JSON file with sample user data for 5 users including name, email, and age"
```

**Expected Plan**:
1. Generate sample user data
2. Write to JSON file
3. Verify file creation

**Generated Script**:
```python
import json

# Create sample user data
users = [
    {"id": 1, "name": "John Doe", "email": "john@example.com", "age": 30},
    {"id": 2, "name": "Jane Smith", "email": "jane@example.com", "age": 25},
    {"id": 3, "name": "Bob Johnson", "email": "bob@example.com", "age": 35},
    {"id": 4, "name": "Alice Williams", "email": "alice@example.com", "age": 28},
    {"id": 5, "name": "Charlie Brown", "email": "charlie@example.com", "age": 32}
]

# Write to JSON file
with open('../output/users.json', 'w') as f:
    json.dump(users, f, indent=2)

print(f"Created users.json with {len(users)} users")
```

**Output File** (`workspace/output/users.json`):
```json
[
  {
    "id": 1,
    "name": "John Doe",
    "email": "john@example.com",
    "age": 30
  },
  ...
]
```

### Example 6: Data Filtering and Sorting

**Task**: Filter and sort data based on criteria.

```bash
dotnet run --project RR.Agent -- "Create a list of 20 random numbers, filter out those less than 50, and sort the remaining in descending order"
```

**Generated Script**:
```python
import random

# Generate 20 random numbers between 1 and 100
numbers = [random.randint(1, 100) for _ in range(20)]
print(f"Original numbers: {numbers}")

# Filter numbers >= 50
filtered = [n for n in numbers if n >= 50]
print(f"\nFiltered (>= 50): {filtered}")

# Sort in descending order
sorted_numbers = sorted(filtered, reverse=True)
print(f"Sorted descending: {sorted_numbers}")
```

## File Operations

### Example 7: Text File Processing

**Task**: Read an external text file and perform operations.

```bash
dotnet run --project RR.Agent -- "Read the file at C:\Users\YourName\document.txt and count the number of words"
```

**Expected Plan**:
1. Copy text file to workspace
2. Read file content
3. Count words
4. Display results

**Generated Script**:
```python
# Read the file
with open('document.txt', 'r') as f:
    content = f.read()

# Count words
words = content.split()
word_count = len(words)

# Count lines
lines = content.split('\n')
line_count = len(lines)

# Count characters
char_count = len(content)

print(f"Word count: {word_count}")
print(f"Line count: {line_count}")
print(f"Character count: {char_count}")
```

### Example 8: File Format Conversion

**Task**: Convert between file formats.

```bash
dotnet run --project RR.Agent -- "Convert data.csv to JSON format"
```

**Generated Script**:
```python
import csv
import json

# Read CSV
with open('data.csv', 'r') as csvfile:
    reader = csv.DictReader(csvfile)
    data = list(reader)

# Write JSON
with open('../output/data.json', 'w') as jsonfile:
    json.dump(data, jsonfile, indent=2)

print(f"Converted {len(data)} records from CSV to JSON")
```

### Example 9: PDF Text Extraction

**Task**: Extract text from PDF files.

```bash
dotnet run --project RR.Agent -- "Extract text from document.pdf at C:\Users\YourName\document.pdf and save it to output.txt"
```

**Expected Plan**:
1. Copy PDF to workspace
2. Install PyPDF2 package
3. Extract text from PDF
4. Save to text file

**Generated Script**:
```python
import PyPDF2

# Open PDF file
with open('document.pdf', 'rb') as pdf_file:
    # Create PDF reader
    pdf_reader = PyPDF2.PdfReader(pdf_file)
    
    # Extract text from all pages
    text = ""
    for page_num in range(len(pdf_reader.pages)):
        page = pdf_reader.pages[page_num]
        text += page.extract_text()
    
    # Save to text file
    with open('../output/output.txt', 'w', encoding='utf-8') as output_file:
        output_file.write(text)
    
    print(f"Extracted {len(text)} characters from {len(pdf_reader.pages)} pages")
```

## Web Scraping

### Example 10: Fetch Web Page Content

**Task**: Download and parse web page content.

```bash
dotnet run --project RR.Agent -- "Fetch the HTML content from https://example.com and save it to a file"
```

**Expected Plan**:
1. Install requests package
2. Fetch web page
3. Save HTML to file

**Generated Script**:
```python
import requests

# Fetch web page
url = "https://example.com"
response = requests.get(url)

# Save HTML content
with open('../output/page.html', 'w', encoding='utf-8') as f:
    f.write(response.text)

print(f"Downloaded {len(response.text)} bytes from {url}")
print(f"Status code: {response.status_code}")
```

### Example 11: Parse and Extract Data

**Task**: Scrape specific data from a website.

```bash
dotnet run --project RR.Agent -- "Fetch news headlines from https://news.ycombinator.com and save them to a JSON file"
```

**Expected Plan**:
1. Install requests and beautifulsoup4
2. Fetch and parse HTML
3. Extract headlines
4. Save to JSON

**Generated Script**:
```python
import requests
from bs4 import BeautifulSoup
import json

# Fetch page
url = "https://news.ycombinator.com"
response = requests.get(url)
soup = BeautifulSoup(response.text, 'html.parser')

# Extract headlines
headlines = []
for item in soup.find_all('span', class_='titleline'):
    link = item.find('a')
    if link:
        headlines.append({
            'title': link.text,
            'url': link['href']
        })

# Save to JSON
with open('../output/headlines.json', 'w') as f:
    json.dump(headlines, f, indent=2)

print(f"Extracted {len(headlines)} headlines")
```

### Example 12: API Integration

**Task**: Fetch data from REST API.

```bash
dotnet run --project RR.Agent -- "Fetch user data from https://jsonplaceholder.typicode.com/users and save to users.json"
```

**Generated Script**:
```python
import requests
import json

# Fetch data from API
url = "https://jsonplaceholder.typicode.com/users"
response = requests.get(url)
users = response.json()

# Save to file
with open('../output/users.json', 'w') as f:
    json.dump(users, f, indent=2)

print(f"Fetched {len(users)} users")
print(f"First user: {users[0]['name']}")
```

## Advanced Use Cases

### Example 13: Multi-Step Data Pipeline

**Task**: Complex data processing pipeline.

```bash
dotnet run --project RR.Agent -- "Read sales.csv, filter sales above $1000, group by category, calculate totals, and create a summary report in JSON"
```

**Expected Plan**:
1. Copy CSV to workspace
2. Install pandas
3. Read and filter data
4. Group and aggregate
5. Create summary report
6. Save to JSON

**Generated Script**:
```python
import pandas as pd
import json

# Read CSV
df = pd.read_csv('sales.csv')
print(f"Total records: {len(df)}")

# Filter sales above $1000
filtered_df = df[df['amount'] > 1000]
print(f"High-value sales: {len(filtered_df)}")

# Group by category and calculate totals
summary = filtered_df.groupby('category').agg({
    'amount': ['sum', 'mean', 'count']
}).round(2)

# Convert to dictionary
summary_dict = summary.to_dict()

# Create report
report = {
    'total_records': len(df),
    'filtered_records': len(filtered_df),
    'summary_by_category': summary_dict,
    'grand_total': filtered_df['amount'].sum()
}

# Save to JSON
with open('../output/sales_report.json', 'w') as f:
    json.dump(report, f, indent=2)

print("Report created successfully")
```

### Example 14: Error Handling and Retry

**Task**: Demonstrate robust error handling.

```bash
dotnet run --project RR.Agent -- "Fetch data from an API with retry logic if it fails"
```

**Generated Script**:
```python
import requests
import time
import json

def fetch_with_retry(url, max_retries=3, delay=2):
    """Fetch URL with retry logic"""
    for attempt in range(max_retries):
        try:
            response = requests.get(url, timeout=10)
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            print(f"Attempt {attempt + 1} failed: {e}")
            if attempt < max_retries - 1:
                print(f"Retrying in {delay} seconds...")
                time.sleep(delay)
            else:
                print("Max retries reached")
                raise

# Fetch data
url = "https://api.example.com/data"
data = fetch_with_retry(url)

# Save results
with open('../output/api_data.json', 'w') as f:
    json.dump(data, f, indent=2)

print("Data fetched and saved successfully")
```

### Example 15: Batch Processing

**Task**: Process multiple files.

```bash
dotnet run --project RR.Agent -- "Find all .txt files in C:\Users\YourName\Documents and count words in each"
```

**Expected Plan**:
1. Find all .txt files
2. Copy them to workspace
3. Process each file
4. Create summary report

**Generated Script**:
```python
import os
import json

# Get all txt files in workspace
txt_files = [f for f in os.listdir('.') if f.endswith('.txt')]

results = {}
for filename in txt_files:
    with open(filename, 'r') as f:
        content = f.read()
        word_count = len(content.split())
        results[filename] = {
            'words': word_count,
            'lines': len(content.split('\n')),
            'characters': len(content)
        }

# Save summary
with open('../output/word_count_summary.json', 'w') as f:
    json.dump(results, f, indent=2)

print(f"Processed {len(txt_files)} files")
for filename, stats in results.items():
    print(f"{filename}: {stats['words']} words")
```

## Best Practices

### Writing Effective Task Descriptions

**Good**:
- ✓ "Read data.csv and calculate the average of the price column"
- ✓ "Extract text from document.pdf and save to output.txt"
- ✓ "Fetch headlines from news.ycombinator.com and save to JSON"

**Too Vague**:
- ✗ "Process some data"
- ✗ "Do something with this file"
- ✗ "Get information from the internet"

**Too Specific** (let the agent figure out implementation):
- ✗ "Use pandas.read_csv with delimiter=',' and encoding='utf-8'"
- ✗ "Import requests, then use requests.get(), then parse with BeautifulSoup"

### Specifying File Paths

- Use absolute paths for external files: `C:\Users\YourName\data.csv`
- For workspace files, the agent handles paths automatically
- Mention file format if ambiguous: "CSV file", "JSON file", "text file"

### Managing Complex Tasks

For very complex tasks:
1. Break into smaller subtasks
2. Run each subtask separately
3. Verify intermediate results
4. Chain outputs as needed

### Handling Failures

If a task fails:
1. Check the execution summary for specific errors
2. Review generated scripts in `workspace/scripts/`
3. Simplify the task or provide more context
4. Ensure external files/URLs are accessible
5. Check that required data formats match expectations
