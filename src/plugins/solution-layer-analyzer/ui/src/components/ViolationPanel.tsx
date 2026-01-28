import React, { useState, useMemo } from 'react';
import {
  Accordion,
  AccordionHeader,
  AccordionItem,
  AccordionPanel,
  Badge,
  Button,
  Text,
  Title3,
} from '@fluentui/react-components';
import { ErrorCircle24Regular, Warning24Regular, Info24Regular } from '@fluentui/react-icons';
import { ViolationItem, SeverityLevel, getSeverityColor } from '../types/analytics';

interface ViolationPanelProps {
  violations: ViolationItem[];
  onViolationClick?: (violation: ViolationItem) => void;
  selectedViolations?: string[];
}

export const ViolationPanel: React.FC<ViolationPanelProps> = ({
  violations,
  onViolationClick,
  selectedViolations = []
}) => {
  const [filterSeverity, setFilterSeverity] = useState<SeverityLevel | 'all'>('all');
  const [sortBy, setSortBy] = useState<'severity' | 'type'>('severity');

  // Group and sort violations
  const groupedViolations = useMemo(() => {
    let filtered = violations;
    if (filterSeverity !== 'all') {
      filtered = violations.filter(v => v.severity === filterSeverity);
    }

    const groups: Record<string, ViolationItem[]> = {};
    
    if (sortBy === 'severity') {
      const severityOrder: SeverityLevel[] = ['critical', 'high', 'medium', 'low'];
      severityOrder.forEach(severity => {
        groups[severity] = filtered.filter(v => v.severity === severity);
      });
    } else {
      filtered.forEach(v => {
        if (!groups[v.type]) {
          groups[v.type] = [];
        }
        groups[v.type].push(v);
      });
    }

    return groups;
  }, [violations, filterSeverity, sortBy]);

  const violationCounts = useMemo(() => {
    const counts: Record<SeverityLevel, number> = {
      critical: 0,
      high: 0,
      medium: 0,
      normal: 0,
      low: 0
    };
    violations.forEach(v => {
      counts[v.severity]++;
    });
    return counts;
  }, [violations]);

  const getSeverityIcon = (severity: SeverityLevel) => {
    switch (severity) {
      case 'critical':
      case 'high':
        return <ErrorCircle24Regular style={{ color: getSeverityColor(severity) }} />;
      case 'medium':
        return <Warning24Regular style={{ color: getSeverityColor(severity) }} />;
      case 'low':
        return <Info24Regular style={{ color: getSeverityColor(severity) }} />;
    }
  };

  if (violations.length === 0) {
    return (
      <div style={{ padding: '20px', textAlign: 'center' }}>
        <Text>No violations detected. Your solution layering looks good! ✓</Text>
      </div>
    );
  }

  return (
    <div style={{ padding: '16px', maxHeight: '800px', overflowY: 'auto' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
        <Title3>Violations</Title3>
        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
          {Object.entries(violationCounts).map(([severity, count]) => (
            count > 0 && (
              <Badge
                key={severity}
                appearance="filled"
                color={severity === 'critical' || severity === 'high' ? 'danger' : 
                       severity === 'medium' ? 'warning' : 'informative'}
                size="small"
              >
                {severity}: {count}
              </Badge>
            )
          ))}
        </div>
      </div>

      {/* Controls */}
      <div style={{ display: 'flex', gap: '8px', marginBottom: '16px', flexWrap: 'wrap' }}>
        <Button
          size="small"
          appearance={filterSeverity === 'all' ? 'primary' : 'secondary'}
          onClick={() => setFilterSeverity('all')}
        >
          All
        </Button>
        <Button
          size="small"
          appearance={filterSeverity === 'critical' ? 'primary' : 'secondary'}
          onClick={() => setFilterSeverity('critical')}
        >
          Critical
        </Button>
        <Button
          size="small"
          appearance={filterSeverity === 'high' ? 'primary' : 'secondary'}
          onClick={() => setFilterSeverity('high')}
        >
          High
        </Button>
        <Button
          size="small"
          appearance={filterSeverity === 'medium' ? 'primary' : 'secondary'}
          onClick={() => setFilterSeverity('medium')}
        >
          Medium
        </Button>
        <Button
          size="small"
          appearance={filterSeverity === 'low' ? 'primary' : 'secondary'}
          onClick={() => setFilterSeverity('low')}
        >
          Low
        </Button>
        <div style={{ flex: 1 }} />
        <Button
          size="small"
          appearance={sortBy === 'severity' ? 'primary' : 'secondary'}
          onClick={() => setSortBy('severity')}
        >
          By Severity
        </Button>
        <Button
          size="small"
          appearance={sortBy === 'type' ? 'primary' : 'secondary'}
          onClick={() => setSortBy('type')}
        >
          By Type
        </Button>
      </div>

      {/* Violations List */}
      <Accordion multiple collapsible>
        {Object.entries(groupedViolations).map(([groupKey, groupViolations]) => {
          if (groupViolations.length === 0) return null;

          const severity = sortBy === 'severity' ? groupKey as SeverityLevel : groupViolations[0].severity;

          return (
            <AccordionItem key={groupKey} value={groupKey}>
              <AccordionHeader
                expandIconPosition="end"
                icon={getSeverityIcon(severity)}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', width: '100%', alignItems: 'center' }}>
                  <Text weight="semibold">
                    {sortBy === 'severity' ? groupKey.toUpperCase() : groupKey}
                  </Text>
                  <Badge appearance="filled" size="small">
                    {groupViolations.length}
                  </Badge>
                </div>
              </AccordionHeader>
              <AccordionPanel>
                {groupViolations.map((violation, idx) => (
                  <div
                    key={idx}
                    style={{
                      padding: '12px',
                      marginBottom: '8px',
                      border: `1px solid ${getSeverityColor(violation.severity)}`,
                      borderLeft: `4px solid ${getSeverityColor(violation.severity)}`,
                      borderRadius: '4px',
                      backgroundColor: selectedViolations.includes(violation.componentId || '') ? '#f5f5f5' : 'white',
                      cursor: onViolationClick ? 'pointer' : 'default',
                    }}
                    onClick={() => onViolationClick?.(violation)}
                  >
                    <div style={{ marginBottom: '8px' }}>
                      <Text weight="semibold">{violation.description}</Text>
                      {violation.componentName && (
                        <Text size={200} style={{ display: 'block', marginTop: '4px', color: '#666' }}>
                          Component: {violation.componentName}
                        </Text>
                      )}
                    </div>
                    {violation.affectedSolutions.length > 0 && (
                      <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap' }}>
                        <Text size={200}>Affected Solutions:</Text>
                        {violation.affectedSolutions.map(sol => (
                          <Badge key={sol} size="small">{sol}</Badge>
                        ))}
                      </div>
                    )}
                    {violation.details && (
                      <Text size={200} style={{ display: 'block', marginTop: '8px', color: '#666', fontStyle: 'italic' }}>
                        {violation.details}
                      </Text>
                    )}
                  </div>
                ))}
              </AccordionPanel>
            </AccordionItem>
          );
        })}
      </Accordion>
    </div>
  );
};
