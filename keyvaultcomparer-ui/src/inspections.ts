export interface InspectionResult {
  ruleName: string;
  severity: 'Low' | 'Medium' | 'High' | 'Critical';
  message: string;
}

export interface SecretMetadata {
  name: string;
  createdOn: string | null;
  updatedOn: string | null;
  expiresOn: string | null;
}

export const analyzeMetadata = (metadata: SecretMetadata): InspectionResult[] => {
  const inspections: InspectionResult[] = [];
  
  if (!metadata.expiresOn) {
    inspections.push({ ruleName: 'No Expiration Date', severity: 'Low', message: 'Secret does not have an expiration date set' });
  }

  if (metadata.updatedOn) {
    const updatedDate = new Date(metadata.updatedOn);
    const now = new Date();
    const diffTime = Math.abs(now.getTime() - updatedDate.getTime());
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24)); 
    if (diffDays > 180) {
      inspections.push({ ruleName: 'Stale Secret', severity: 'Medium', message: `Secret has not been updated in ${diffDays} days (> 180 days)` });
    }
  }

  return inspections;
};

export const calculateEntropy = (str: string): number => {
  if (!str) return 0;
  const len = str.length;
  const frequencies = new Map<string, number>();
  for (let i = 0; i < len; i++) {
    const char = str[i];
    frequencies.set(char, (frequencies.get(char) || 0) + 1);
  }
  let entropy = 0;
  for (const count of frequencies.values()) {
    const p = count / len;
    entropy -= p * Math.log2(p);
  }
  return entropy;
};

export const DANGEROUS_PATTERNS = [
  // Original
  { regex: /-----BEGIN (RSA |DSA |EC |OPENSSH |PGP )?PRIVATE KEY-----/, name: 'Private Key', severity: 'Critical' as const },
  { regex: /^eyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+$/, name: 'JWT Token', severity: 'High' as const },
  { regex: /AccountKey=[a-zA-Z0-9+/=]{40,}/, name: 'Azure Storage Key', severity: 'Critical' as const },
  { regex: /xox[baprs]-[0-9a-zA-Z]{10,}/, name: 'Slack Token', severity: 'High' as const },
  { regex: /AIza[0-9A-Za-z-_]{35}/, name: 'Google API Key', severity: 'High' as const },
  // Expanded
  { regex: /ghp_[a-zA-Z0-9]{36}/, name: 'GitHub PAT', severity: 'High' as const },
  { regex: /(AKIA|A3T|AGPA|AIDA|AROA|AIPA|ANPA|ANVA|ASIA)[A-Z0-9]{16}/, name: 'AWS Access Key', severity: 'High' as const },
  { regex: /[r|s]k_(live|test)_[a-zA-Z0-9]{24}/, name: 'Stripe Key', severity: 'High' as const },
  { regex: /npm_[a-zA-Z0-9]{36}/, name: 'NPM Access Token', severity: 'Medium' as const },
  { regex: /sk-[a-zA-Z0-9]{48}/, name: 'OpenAI API Key', severity: 'High' as const },
  { regex: /mongodb(\+srv)?:\/\/[^\s]+:[^\s]+@[^\s]+/, name: 'MongoDB Connection String', severity: 'Critical' as const },
  { regex: /postgres(ql)?:\/\/[^\s]+:[^\s]+@[^\s]+/, name: 'PostgreSQL Connection String', severity: 'Critical' as const },
  { regex: /mysql:\/\/[^\s]+:[^\s]+@[^\s]+/, name: 'MySQL Connection String', severity: 'Critical' as const },
  { regex: /sq0[a-z]{2}-[0-9A-Za-z\-_]{22,43}/, name: 'Square Access Token', severity: 'High' as const },
  { regex: /ya29\.[0-9A-Za-z_-]+/, name: 'Google OAuth Access Token', severity: 'High' as const },
  { regex: /SG\.[0-9A-Za-z-_]{22}\.[0-9A-Za-z-_]{43}/, name: 'SendGrid API Key', severity: 'High' as const },
];

export const IGNORED_VALUES = new Set(['true', 'false', '0', '1', 'yes', 'no']);

export const analyzeSecret = (name: string, value: string): { inspections: InspectionResult[], highestSeverity: 'Low' | 'Medium' | 'High' | 'Critical' | undefined } => {
  const inspections: InspectionResult[] = [];
  
  // Name Inspections
  if (name.length > 60) {
    inspections.push({ ruleName: 'Suspicious Name (Length)', severity: 'Medium', message: `Name is exceptionally long (${name.length} chars). Consider refactoring.` });
  }
  
  const guidRegex = /^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$/i;
  if (guidRegex.test(name)) {
    inspections.push({ ruleName: 'Suspicious Name (GUID)', severity: 'Medium', message: 'Name is a UUID/GUID. This degrades observability and manual rotation.' });
  } else {
    const nameEntropy = calculateEntropy(name);
    if (nameEntropy > 4.2 && name.length > 20) {
      inspections.push({ ruleName: 'Suspicious Name (High Entropy)', severity: 'High', message: `Name has high entropy (${nameEntropy.toFixed(2)}). Ensure sensitive data is not leaked in the name.` });
    }
  }

  const getHighestSeverity = (): 'Low' | 'Medium' | 'High' | 'Critical' | undefined => {
    let highest: 'Low' | 'Medium' | 'High' | 'Critical' | undefined = undefined;
    const severityScore = { 'Low': 1, 'Medium': 2, 'High': 3, 'Critical': 4 };
    let maxScore = 0;
    for (const ins of inspections) {
      const score = severityScore[ins.severity];
      if (score > maxScore) {
        maxScore = score;
        highest = ins.severity;
      }
    }
    return highest;
  };

  if (!value || IGNORED_VALUES.has(value.toLowerCase())) return { inspections, highestSeverity: getHighestSeverity() };

  for (const pattern of DANGEROUS_PATTERNS) {
    if (pattern.regex.test(value)) {
      inspections.push({ ruleName: 'Dangerous Pattern', severity: pattern.severity, message: `Matched known pattern: ${pattern.name}` });
    }
  }

  if (value.startsWith('http://')) {
    inspections.push({ ruleName: 'Insecure Protocol', severity: 'Medium', message: 'Uses unencrypted HTTP protocol' });
  }

  if (value !== value.trim() && value.trim().length > 0) {
    inspections.push({ ruleName: 'Whitespace Detected', severity: 'Low', message: 'Contains leading or trailing whitespace' });
  }

  const nameLower = name.toLowerCase();
  const isPasswordLike = ['password', 'pwd', 'secret', 'key', 'token'].some(k => nameLower.includes(k));
  
  if (isPasswordLike) {
    const entropy = calculateEntropy(value);
    if (value.length < 8) {
      inspections.push({ ruleName: 'Weak Secret', severity: 'High', message: 'Password-like secret is too short (< 8 chars)' });
    } else if (entropy < 3.0) {
      inspections.push({ ruleName: 'Low Entropy', severity: 'High', message: `Password-like secret has low entropy (${entropy.toFixed(2)})` });
    }
    const weakPasswords = ['123456', 'password', 'test', 'admin', 'changeme'];
    if (weakPasswords.includes(value.toLowerCase())) {
      inspections.push({ ruleName: 'Weak Secret', severity: 'Critical', message: 'Secret uses a highly compromised generic value' });
    }
  } else {
    const entropy = calculateEntropy(value);
    if (entropy < 2.0 && value.length > 5 && !value.includes('http')) {
      inspections.push({ ruleName: 'Low Entropy', severity: 'Low', message: `Unusually low entropy (${entropy.toFixed(2)})` });
    }
  }

  return { inspections, highestSeverity: getHighestSeverity() };
};
