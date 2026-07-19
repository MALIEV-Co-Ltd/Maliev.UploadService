import re

with open('ExceptionHandlingMiddlewareTests.cs', 'r') as f:
    content = f.read()

# Pattern to find ExceptionHandlingMiddleware instantiations without environment parameter
pattern = r'(new ExceptionHandlingMiddleware\([^;]*?logger: _mockLogger\.Object)\s*\)'

# Replace with environment parameter added
replacement = r'\1,\n            environment: _mockEnvironment.Object)'

content = re.sub(pattern, replacement, content)

with open('ExceptionHandlingMiddlewareTests.cs', 'w') as f:
    f.write(content)

print("Fixed all middleware instantiations")
