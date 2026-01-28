import React, { useState, useMemo } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  Button,
  Input,
  Text,
  Badge,
  makeStyles,
  tokens,
  Switch,
} from '@fluentui/react-components';
import { DismissRegular, SearchRegular } from '@fluentui/react-icons';
import { Editor } from '@monaco-editor/react';

const useStyles = makeStyles({
  dialogSurface: {
    maxWidth: '900px',
    width: '90vw',
    maxHeight: '85vh',
  },
  searchBar: {
    marginBottom: tokens.spacingVerticalM,
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
  },
  statsText: {
    marginBottom: tokens.spacingVerticalM,
    color: tokens.colorNeutralForeground3,
  },
  attributeList: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    maxHeight: '60vh',
    overflowY: 'auto',
  },
  attributeItem: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  attributeHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalS,
  },
  attributeKey: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase400,
  },
  attributeValue: {
    padding: tokens.spacingVerticalS,
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusSmall,
    fontFamily: tokens.fontFamilyMonospace,
    fontSize: tokens.fontSizeBase200,
    wordBreak: 'break-all',
  },
  changedBadge: {
    backgroundColor: tokens.colorPaletteYellowBackground2,
  },
});

interface LayerViewerDialogProps {
  isOpen: boolean;
  onClose: () => void;
  solutionName: string;
  componentJson: string;
  changedAttributesJson?: string; // msdyn_changes field
}

export const LayerViewerDialog: React.FC<LayerViewerDialogProps> = ({
  isOpen,
  onClose,
  solutionName,
  componentJson,
  changedAttributesJson,
}) => {
  const styles = useStyles();
  const [searchTerm, setSearchTerm] = useState('');
  const [showChangedOnly, setShowChangedOnly] = useState(true);

  // Parse the component JSON to extract attributes
  const parseAttributes = (jsonText: string): Record<string, any> => {
    try {
      const parsed = JSON.parse(jsonText);
      if (!parsed.Attributes || !Array.isArray(parsed.Attributes)) {
        return {};
      }
      const attrs: Record<string, any> = {};
      parsed.Attributes.forEach((attr: any) => {
        if (attr.Key) {
          attrs[attr.Key] = attr.Value;
        }
      });
      return attrs;
    } catch (error) {
      console.error('Failed to parse component JSON:', error);
      return {};
    }
  };

  // Parse changed attributes from msdyn_changes
  const parseChangedAttributes = (changesJson: string | undefined): Set<string> => {
    if (!changesJson) return new Set();
    try {
      const parsed = JSON.parse(changesJson);
      // msdyn_changes typically contains an array of changed attribute names
      if (Array.isArray(parsed)) {
        return new Set(parsed);
      } else if (parsed.changedAttributes && Array.isArray(parsed.changedAttributes)) {
        return new Set(parsed.changedAttributes);
      }
      return new Set();
    } catch (error) {
      console.error('Failed to parse changed attributes:', error);
      return new Set();
    }
  };

  const attributes = useMemo(() => parseAttributes(componentJson), [componentJson]);
  const changedAttributes = useMemo(() => parseChangedAttributes(changedAttributesJson), [changedAttributesJson]);

  // Determine if value is complex (needs Monaco editor)
  const isComplexValue = (value: any): boolean => {
    if (value === null || value === undefined) return false;
    if (typeof value === 'object') return true;
    if (typeof value === 'string' && value.length > 200) return true;
    if (typeof value === 'string' && (value.trim().startsWith('<') || value.trim().startsWith('{'))) return true;
    return false;
  };

  // Filter and sort attributes
  const displayedAttributes = useMemo(() => {
    let attrs = Object.entries(attributes);

    // Filter by changed only if enabled and we have changed attributes data
    if (showChangedOnly && changedAttributes.size > 0) {
      attrs = attrs.filter(([key]) => changedAttributes.has(key));
    }

    // Filter by search term
    if (searchTerm) {
      const term = searchTerm.toLowerCase();
      attrs = attrs.filter(([key, value]) => {
        const keyMatch = key.toLowerCase().includes(term);
        const valueMatch = JSON.stringify(value).toLowerCase().includes(term);
        return keyMatch || valueMatch;
      });
    }

    // Sort alphabetically by key
    attrs.sort(([a], [b]) => a.localeCompare(b));

    return attrs;
  }, [attributes, searchTerm, showChangedOnly, changedAttributes]);

  // Render simple value
  const renderSimpleValue = (value: any): string => {
    if (value === null || value === undefined) return '(null)';
    if (typeof value === 'boolean') return value ? 'true' : 'false';
    if (typeof value === 'number') return value.toString();
    if (typeof value === 'string') return value;
    return JSON.stringify(value, null, 2);
  };

  // Detect language for Monaco
  const detectLanguage = (value: any): string => {
    if (typeof value !== 'string') return 'json';
    const trimmed = value.trim();
    if (trimmed.startsWith('<')) return 'xml';
    if (trimmed.startsWith('{') || trimmed.startsWith('[')) return 'json';
    return 'plaintext';
  };

  return (
    <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface className={styles.dialogSurface}>
        <DialogTitle>
          View Layer: {solutionName}
        </DialogTitle>
        <DialogBody>
          <div className={styles.searchBar}>
            <Input
              placeholder="Search by key or value..."
              contentBefore={<SearchRegular />}
              value={searchTerm}
              onChange={(_, data) => setSearchTerm(data.value)}
              style={{ flex: 1 }}
            />
            {changedAttributes.size > 0 && (
              <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                <Switch
                  checked={showChangedOnly}
                  onChange={(_, data) => setShowChangedOnly(data.checked)}
                  label="Changed attributes only"
                />
                <Badge appearance="filled" color="warning">
                  {changedAttributes.size} changed
                </Badge>
              </div>
            )}
          </div>

          <Text className={styles.statsText}>
            Showing {displayedAttributes.length} of {Object.keys(attributes).length} attributes
          </Text>

          <div className={styles.attributeList}>
            {displayedAttributes.length === 0 ? (
              <Text style={{ color: tokens.colorNeutralForeground3, textAlign: 'center', padding: tokens.spacingVerticalXL }}>
                {searchTerm ? 'No attributes match your search.' : 'No attributes available.'}
              </Text>
            ) : (
              displayedAttributes.map(([key, value]) => {
                const isComplex = isComplexValue(value);
                const isChanged = changedAttributes.has(key);

                return (
                  <div key={key} className={styles.attributeItem}>
                    <div className={styles.attributeHeader}>
                      <Text className={styles.attributeKey}>{key}</Text>
                      <div style={{ display: 'flex', gap: tokens.spacingHorizontalXS }}>
                        {isChanged && (
                          <Badge appearance="filled" color="warning">
                            Changed
                          </Badge>
                        )}
                        {isComplex && (
                          <Badge appearance="outline">Complex</Badge>
                        )}
                      </div>
                    </div>

                    {isComplex ? (
                      <Editor
                        height="300px"
                        language={detectLanguage(value)}
                        value={typeof value === 'string' ? value : JSON.stringify(value, null, 2)}
                        theme="vs-dark"
                        options={{
                          readOnly: true,
                          minimap: { enabled: false },
                          scrollBeyondLastLine: false,
                          fontSize: 12,
                          lineNumbers: 'on',
                          wordWrap: 'on',
                        }}
                      />
                    ) : (
                      <div className={styles.attributeValue}>
                        {renderSimpleValue(value)}
                      </div>
                    )}
                  </div>
                );
              })
            )}
          </div>
        </DialogBody>
        <DialogActions>
          <Button appearance="secondary" onClick={onClose} icon={<DismissRegular />}>
            Close
          </Button>
        </DialogActions>
      </DialogSurface>
    </Dialog>
  );
};
